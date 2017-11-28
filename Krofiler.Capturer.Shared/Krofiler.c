#include <stdio.h>
#include <dlfcn.h>
#include <execinfo.h>
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
#define HS_FLAGS_ROOT_EVENTS 8
#define HS_FLAGS_ROOT_REGISTER_STACKTRACE 16

#define FILE_FORMAT_VERSION 1

#define DATA_TYPE_ROOT 1
#define DATA_TYPE_HEAP_OBJECT 2
#define DATA_TYPE_ALLOC 3
#define DATA_TYPE_GC_MOVE 4
#define DATA_TYPE_MORE_REFERENCES 5
#define DATA_TYPE_CLASS_INFO 6
#define DATA_TYPE_HEAPSHOT_START 7
#define DATA_TYPE_HEAPSHOT_END 8
#define DATA_TYPE_ROOT_REGISTER 9
#define DATA_TYPE_ROOT_UNREGISTER 10
#define DATA_TYPE_METHOD_JIT 11

#define ADDITIONAL_DATA_STRING_VALUE 1

int count11=0;
mono_mutex_t writeLock;
#define LOCK_WRITE do { mono_os_mutex_lock (&writeLock); count11++; } while (0)
#define UNLOCK_WRITE do { count11--; mono_os_mutex_unlock (&writeLock);  } while (0)

FILE *outputFile = NULL;
void *class_cache;
int currentId = 0;
int heapshot_flags = 0;
MonoProfilerHandle ba;

static void
gc_handle (MonoProfiler *prof, int op, int type, uintptr_t handle, MonoObject *obj)
{
}

static void writeByte(char value)
{
    g_assert(count11==1);
	putc(value, outputFile);
}

static void writeShort(int value)
{
    g_assert(count11==1);
	writeByte(value);
	writeByte(value>>8);
}

static void writeInt(int value)
{
    g_assert(count11==1);
	fwrite(&value, sizeof(int), 1, outputFile);
}


static void writeUintptr_t(uintptr_t value)
{
    g_assert(count11==1);
	fwrite(&value, sizeof(uintptr_t), 1, outputFile);
}

static void writePointer(void* value)
{
    g_assert(count11==1);
	fwrite(&value, sizeof(void*), 1, outputFile);
}

static void writeString(const char* value)
{
    g_assert(count11==1);
	if (value == NULL) {
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
gc_roots (MonoProfiler *prof, uint64_t num, const mono_byte *const *addresses, const MonoObject* const *objects)
{
	//TODO: Try to do GC_ROOTS at same time as heap walk 
	LOCK_WRITE;
	writeByte(DATA_TYPE_ROOT);
	writeInt(num);
	for (int i = 0; i < num; ++i) {
        writePointer(objects[i]);
        writePointer(addresses[i]);
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
    writePointer(vtable->klass);
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
		writeByte(num);//In mono its harded to maximum 128 refs per loop
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
gc_event (MonoProfiler *profiler, MonoProfilerGCEvent ev, uint32_t generation)
{
	if (ev == MONO_GC_EVENT_PRE_START_WORLD) {
		LOCK_WRITE;
		writeByte(DATA_TYPE_HEAPSHOT_START);
		mono_gc_walk_heap(0, gc_reference, NULL);

		//For some reason this doesn't work :/
		//mono_domain_foreach does gc allocation and we are under GC_LOCK...
		for(int i=0;i<100;i++) {//TODO: Handle this better, its possible someone has 100+ domains
			MonoDomain* domain = mono_domain_get_by_id(i);
			if (domain != NULL)
				walk_domain (domain, NULL);
		}

		mono_assembly_foreach (walk_assembly, mono_get_root_domain());
		writeByte(DATA_TYPE_HEAPSHOT_END);
		fflush(outputFile);
		UNLOCK_WRITE;
	}
}

void
print_trace (int skip)
{
  void *array[250];
  size_t size;
  //char **strings;
  size_t i;

  size = backtrace (array, 250);
  //strings = backtrace_symbols (array, size);
  writeByte(size-skip);
  for (i = skip; i < size; i++){
    writePointer(array[i]);
    writeString ("");
  }

  //free (strings);
}

static void
gc_alloc (MonoProfiler *prof, MonoObject *obj)
{
	LOCK_WRITE;
	writeByte(DATA_TYPE_ALLOC);
	writePointer(obj);
	if (heapshot_flags & HS_FLAGS_ALLOCATIONS_STACKTRACE) {
        print_trace(2);
	}
	UNLOCK_WRITE;
}

static void
gc_moves (MonoProfiler *prof, MonoObject *const *objects, uint64_t num)
{
	if ((heapshot_flags & HS_FLAGS_MOVES) == 0)
		return;
	LOCK_WRITE;
    g_assert(num<256);
	writeByte(DATA_TYPE_GC_MOVE);
	writeByte(num);//Struct of moves is limited to 64
	for (int i = 0; i < num; ++i)
		writePointer (objects [i]);
	UNLOCK_WRITE;
}

static void
gc_root_register (MonoProfiler *prof, const mono_byte *start, size_t size, MonoGCRootSource kind, const void *key, const char *msg)
{
    LOCK_WRITE;
    writeByte(DATA_TYPE_ROOT_REGISTER);
    writePointer(start);
    writeInt(size);
    writeByte(kind);
    writePointer(key);
    writeString(msg);
    if (heapshot_flags & HS_FLAGS_ROOT_REGISTER_STACKTRACE) {
        //mono_stack_walk_no_il (walk_stack, NULL);
        print_trace(3);
    }
    UNLOCK_WRITE;
}

static void
gc_root_deregister (MonoProfiler *prof, const mono_byte *start)
{
    LOCK_WRITE;
    writeByte(DATA_TYPE_ROOT_UNREGISTER);
    writePointer(start);
    UNLOCK_WRITE;
}

void krofiler_stop()
{
	if (outputFile==NULL)
		return;

    mono_profiler_set_gc_allocation_callback (ba, NULL);
    mono_profiler_set_gc_moves_callback (ba, NULL);
    mono_profiler_set_jit_done_callback (ba, NULL);
    mono_profiler_set_gc_root_register_callback (ba, NULL);
    mono_profiler_set_gc_root_unregister_callback (ba, NULL);
	heapshot_flags = 0;
	LOCK_WRITE;
	fclose(outputFile);
    outputFile = NULL;
	UNLOCK_WRITE;
	//Probably need to clean up more things
}

static void
sample_shutdown (void* prof)
{
	krofiler_stop();
}

static void jit_done (MonoProfiler *prof, MonoMethod *method, MonoJitInfo *jinfo)
{
    char* methodName=mono_method_full_name(method, TRUE);
    LOCK_WRITE;
    writeByte(DATA_TYPE_METHOD_JIT);
    writeString(methodName);
    writePointer(jinfo->code_start);
    writeInt(jinfo->code_size);
    UNLOCK_WRITE;
}

int krofiler_start(char *path, int flags)
{
	if (outputFile != NULL)
        return 1;
    heapshot_flags = flags;
    mono_os_mutex_init (&writeLock);
    LOCK_WRITE;
    outputFile=fopen(path, "w");
    writeString("Krofiler");//MAGIC STRING
    writeShort(FILE_FORMAT_VERSION);
    writeInt(flags);
    writeByte(sizeof(void*));
    UNLOCK_WRITE;
    ba = mono_profiler_create (NULL);
    if (heapshot_flags & HS_FLAGS_ROOT_EVENTS){
        mono_profiler_set_gc_root_register_callback (ba, gc_root_register);
        mono_profiler_set_gc_root_unregister_callback (ba, gc_root_deregister);
    }
    if (heapshot_flags & HS_FLAGS_ALLOCATIONS){
        mono_profiler_enable_allocations();
        mono_profiler_set_gc_allocation_callback (ba, gc_alloc);
    }
    if (heapshot_flags & HS_FLAGS_MOVES)
        mono_profiler_set_gc_moves_callback (ba, gc_moves);
    mono_profiler_set_jit_done_callback (ba, jit_done);
    return 0;
}

void
krofiler_take_heapshot ()
{
	class_cache	 = mono_conc_hashtable_new (NULL, NULL);
	currentId = 0;
	getMonoClassId(mono_class_vtable (mono_get_root_domain(), mono_get_object_class()));//Make sure System.Object is always Id=1
	getMonoClassId(mono_class_vtable (mono_get_root_domain(), mono_get_string_class()));//Make sure System.String is always Id=2
   
	//Clean up garbage
	mono_gc_collect(1);
    mono_profiler_set_gc_event_callback (ba, gc_event);
	//We will collect all Roots and do heapWalk at MONO_GC_EVENT_PRE_START_WORLD
    mono_profiler_set_gc_roots_callback (ba, gc_roots);
	mono_gc_collect(1);
    mono_profiler_set_gc_roots_callback (ba, NULL);
    mono_profiler_set_gc_event_callback (ba, NULL);
	mono_conc_hashtable_destroy (class_cache);
}

void
mono_profiler_init_krofiler (const char *desc)
{
    //krofiler_start(desc+9, HS_FLAGS_ROOT_EVENTS | HS_FLAGS_ROOT_REGISTER_STACKTRACE);
    krofiler_start(desc+9, HS_FLAGS_ROOT_EVENTS | HS_FLAGS_ROOT_REGISTER_STACKTRACE | HS_FLAGS_ALLOCATIONS | HS_FLAGS_MOVES | HS_FLAGS_ALLOCATIONS_STACKTRACE);
}