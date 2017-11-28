using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin(
	"Krofiler",
	Namespace = "MonoDevelop",
	Version = "0.1"
)]

[assembly: AddinName("Krofiler")]
[assembly: AddinCategory("IDE extensions")]
[assembly: AddinDescription("Adds ability to take heapshots on the fly.")]
[assembly: AddinAuthor("David Karlaš")]
[assembly: AddinDependency("PerformanceDiagnostics", "7.0")]
[assembly: AddinDependency("Core", "7.0")]
[assembly: AddinDependency("Ide", "7.0")]
