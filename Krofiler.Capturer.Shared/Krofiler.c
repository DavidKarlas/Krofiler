#include <stdio.h>
#include <dlfcn.h>
#include <mono/metadata/profiler.h>
#include <mono/metadata/object.h>
#include <pthread.h>
#include <mono/metadata/sgen-mono.c>
#include <mono/sgen/sgen-gc.c>
#include <mono/mini/jit.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/mono-config.h>
#include <mono/metadata/object-internals.h>
#define LOG(...) do { fprintf (stdout, "> " __VA_ARGS__); fprintf (stdout, "\n"); } while (0)

//#define DEBUG

#define HS_FLAGS_ALLOCATIONS 1
#define HS_FLAGS_MOVES 2
#define HS_FLAGS_ALLOCATIONS_STACKTRACE 4

#define FILE_FORMAT_VERSION 1

#define DATA_TYPE_ROOT 1
#define DATA_TYPE_HEAP_OBJECT 2
#define DATA_TYPE_ALLOC 3
#define DATA_TYPE_GC_MOVE 4
#define DATA_TYPE_MORE_REFERENCES 5
#define DATA_TYPE_CLASS_INFO 6
#define DATA_TYPE_HEAPSHOT_START 7
#define DATA_TYPE_HEAPSHOT_END 8

#define ADDITIONAL_DATA_STRING_VALUE 1

mono_mutex_t writeLock;
#define LOCK_WRITE do { mono_os_mutex_lock (&writeLock); } while (0)
#define UNLOCK_WRITE do { mono_os_mutex_unlock (&writeLock); } while (0)

FILE *outputFile = NULL;
void *class_cache;
int currentId = 0;
int heapshot_flags = 0;

static void
gc_handle (MonoProfiler *prof, int op, int type, uintptr_t handle, MonoObject *obj)
{
}

static void writeByte(char value)
{
	putc(value, outputFile);
}

static void writeShort(int value)
{
	writeByte(value);
	writeByte(value>>8);
}

static void writeInt(int value)
{
	fwrite(&value, sizeof(int), 1, outputFile);
}


static void writeUintptr_t(uintptr_t value)
{
	fwrite(&value, sizeof(uintptr_t), 1, outputFile);
}

static void writePointer(void* value)
{
	fwrite(&value, sizeof(void*), 1, outputFile);
}

static void writeString(const char* value)
{
	if(value == NULL) {
		writeByte(0);
		return;
	}
	int len = strlen(value);
	if(len > 1000)
		len = 1000;
	int l = len;
	while (l >= 0x80) {
		writeByte(0xFF & (l | 0x80));
		l >>= 7;
	}
	writeByte(l);
	fwrite(value, len, 1, outputFile);
}

static mono_bool
walk_stack (MonoMethod *method, int32_t native_offset, int32_t il_offset, mono_bool managed, void* data)
{
	if(method)
		writeString(method->name);
	else
		writeString("null");
	return FALSE;
}

static void
gc_roots (MonoProfiler *prof, int num, void **objects, int *root_types, uintptr_t *extra_info)
{
	//TODO: Try to do GC_ROOTS at same time as heap walk 
	LOCK_WRITE;
	writeByte(DATA_TYPE_ROOT);
	writeByte(num);
	for (int i = 0; i < num; ++i) {
		writePointer(objects[i]);
		//writeInt(root_types[i]);
		//writeUintptr_t(extra_info[i]);
	}
	UNLOCK_WRITE;
}

#define FIELD_ATTRIBUTE_STATIC                0x0010
#define FIELD_ATTRIBUTE_INIT_ONLY             0x0020
#define FIELD_ATTRIBUTE_LITERAL               0x0040

static int
getMonoClassId(MonoVTable *vtable)
{
	if(vtable->klass == mono_get_object_class())
		return 1;
	if(vtable->klass == mono_get_string_class())
		return 2;
	int id = GPOINTER_TO_INT (mono_conc_hashtable_lookup (class_cache, vtable));
	if (id)
		return id;
	int parentId = 0;
	if(vtable->klass->parent){
		if(vtable->klass->parent == mono_get_object_class()) //Optimisation
			parentId = 1;
		else
			parentId = getMonoClassId (mono_class_try_get_vtable (vtable->domain, vtable->klass->parent));
	}
	currentId++;
	writeByte(DATA_TYPE_CLASS_INFO);
	writeShort(currentId);
	writeShort(parentId);
	char* name=mono_type_get_name (mono_class_get_type (vtable->klass));
	writeString(name);
	mono_free(name);

	writeShort(mono_class_num_fields (vtable->klass));

	MonoClassField *field;
	gpointer iter = NULL;
	while ((field = mono_class_get_fields (vtable->klass, &iter))) {
		writeString(mono_field_get_name (field));
		writeShort(field->offset);
		int fieldFlags = mono_field_get_flags (field);
		writeShort(fieldFlags);
		if((fieldFlags & FIELD_ATTRIBUTE_STATIC) && !(fieldFlags & FIELD_ATTRIBUTE_LITERAL) && mono_type_is_reference(field->type)) {
			MonoObject* o;
			MonoError error;
			mono_field_static_get_value (vtable, field, &o);
			if (!is_ok(&error)) {
				writePointer(0);
				//TODO: cleanup error
			} else {
				writePointer((void*)o);
			}
		} else {
			writePointer(0);
		}
	}
	mono_conc_hashtable_insert (class_cache, vtable, GINT_TO_POINTER(currentId));
	return currentId;
}

static int
gc_reference (MonoObject *obj, MonoClass *klass, uintptr_t size, uintptr_t num, MonoObject **refs, uintptr_t *offsets, void *data)
{
	if (size == 0) {
		writeByte(DATA_TYPE_MORE_REFERENCES);
		writePointer(obj);
		writeByte(num);//In mono it's harded to maximum 128 refs per loop
		for (int i = 0; i < num; ++i) {
			writePointer(refs[i]);
			writeShort(offsets[i]);
		}
		return 0;
	}
	int classId = getMonoClassId(obj->vtable);
	writeByte(DATA_TYPE_HEAP_OBJECT);
	writePointer(obj);
	writeShort(classId);
	writeShort(size);
	writeByte(num);
	for (int i = 0; i < num; ++i) {
		writePointer(refs[i]);
		writeShort(offsets[i]);
	}
	if (classId == 2) {
		char* str = mono_string_to_utf8((MonoString*)obj);
		writeString(str);
		mono_free(str);
	}
	return 0;
}

MonoVTable *
mono_class_try_get_vtable (MonoDomain *domain, MonoClass *klass)
{
	MonoClassRuntimeInfo *runtime_info;

	g_assert (klass);

	runtime_info = klass->runtime_info;
	if (runtime_info && runtime_info->max_domain >= domain->domain_id && runtime_info->domain_vtables [domain->domain_id])
		return runtime_info->domain_vtables [domain->domain_id];
	return NULL;
}

static void
walk_assembly (MonoAssembly* assembly, void* user_data)
{
	MonoDomain* domain=(MonoDomain*)user_data;
	MonoImage *image = mono_assembly_get_image (assembly);
	MonoInternalHashTable *class_cache = &image->class_cache;
	for (int i = 0; i < class_cache->size; i ++) {
		MonoClass *klass;

		for (klass = class_cache->table [i]; klass != NULL; klass = *(class_cache->next_value (klass))) {
			MonoVTable* vtable = mono_class_try_get_vtable (domain, klass);
			if (vtable != NULL)
				getMonoClassId(vtable);
		}
	}
}

static void
walk_domain (MonoDomain* domain, void *user_data)
{
	GSList* tmp;
	MonoAssembly* assembly;
	for (tmp = domain->domain_assemblies; tmp; tmp = tmp->next) {
		walk_assembly((MonoAssembly*)tmp->data, domain);
	}
}

static void
gc_event (MonoProfiler *profiler, MonoGCEvent ev, int generation)
{
	if (ev == MONO_GC_EVENT_PRE_START_WORLD) {
		LOCK_WRITE;
		writeByte(DATA_TYPE_HEAPSHOT_START);
		mono_gc_walk_heap(0, gc_reference, NULL);

		//For some reason this doesn't work :/
		//mono_domain_foreach does gc allocation and we are under GC_LOCK...
		for(int i=0;i<100;i++) {//TODO: Handle this better, it's possible someone has 100+ domains
			MonoDomain* domain = mono_domain_get_by_id(i);
			if (domain != NULL)
				walk_domain (domain, NULL);
		}

		//mono_assembly_foreach (walk_assembly, mono_get_root_domain());
		writeByte(DATA_TYPE_HEAPSHOT_END);
		fflush(outputFile);
		UNLOCK_WRITE;
	}
}

static void
gc_resize (MonoProfiler *profiler, int64_t new_size)
{
}

static void
gc_alloc (MonoProfiler *prof, MonoObject *obj, MonoClass *klass)
{
	if ((heapshot_flags & HS_FLAGS_ALLOCATIONS) == 0)
		return;
	LOCK_WRITE;
	writeByte(DATA_TYPE_ALLOC);
	writePointer(obj);
	if (heapshot_flags & HS_FLAGS_ALLOCATIONS_STACKTRACE) {
		mono_stack_walk_no_il (walk_stack, NULL);
		writeString("");
	}
	UNLOCK_WRITE;
}

static void
gc_moves (MonoProfiler *prof, void **objects, int num)
{
	if ((heapshot_flags & HS_FLAGS_MOVES) == 0)
		return;
	LOCK_WRITE;
	writeByte(DATA_TYPE_GC_MOVE);
	writeByte(num);//Struct of moves is limited to 64
	for (int i = 0; i < num; ++i)
		writePointer (objects [i]);
	UNLOCK_WRITE;
}

void krofiler_stop()
{
	if (outputFile==NULL)
		return;
	heapshot_flags = 0;
	LOCK_WRITE;
	fclose(outputFile);
	UNLOCK_WRITE;
	outputFile = NULL;
	//Probably need to clean up more things
}

static void
sample_shutdown (void* prof)
{
	krofiler_stop();
}

int krofiler_start(char *path, int flags)
{
	if (outputFile != NULL)
		return 1;
	heapshot_flags = flags;
	mono_os_mutex_init (&writeLock);
	outputFile=fopen(path, "w");
	writeString("Krofiler");//MAGIC STRING
	writeShort(FILE_FORMAT_VERSION);
	writeInt(flags);
	writeByte(sizeof(void*));
	mono_profiler_install (NULL, sample_shutdown);
	mono_profiler_install_gc (gc_event, gc_resize);
	mono_profiler_install_gc_roots (gc_handle, gc_roots);
	if (heapshot_flags & HS_FLAGS_ALLOCATIONS)
		mono_profiler_install_allocation (gc_alloc);
	if (heapshot_flags & HS_FLAGS_MOVES)
		mono_profiler_install_gc_moves (gc_moves);
	return 0;
}

void
krofiler_take_heapshot ()
{
	class_cache	 = mono_conc_hashtable_new (NULL, NULL);
	currentId = 0;
	getMonoClassId(mono_class_vtable (mono_get_root_domain(), mono_get_object_class()));//Make sure System.Object is always Id=1
	getMonoClassId(mono_class_vtable (mono_get_root_domain(), mono_get_string_class()));//Make sure System.String is always Id=2

	int profilerPersistantFlags = 0;
	if (heapshot_flags | HS_FLAGS_ALLOCATIONS)
		profilerPersistantFlags |= MONO_PROFILE_ALLOCATIONS;
	if (heapshot_flags | HS_FLAGS_MOVES)
		profilerPersistantFlags |= MONO_PROFILE_GC_MOVES;

	int profilerHeapshotFlags = MONO_PROFILE_GC_ROOTS | MONO_PROFILE_GC;

	//Clean up garbage
	mono_gc_collect(1);

	//Start listening for events
	mono_profiler_set_events (profilerHeapshotFlags | profilerPersistantFlags);

	//We will collect all Roots and do heapWalk at MONO_GC_EVENT_PRE_START_WORLD
	mono_gc_collect(1);

	//Keep tracking allocations and moves so we can make diffs between heapshots
	mono_profiler_set_events (profilerPersistantFlags);
	mono_conc_hashtable_destroy (class_cache);
}