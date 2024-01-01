using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace ILRepacking
{
    public class RepackAssemblyResolver : DefaultAssemblyResolver
    {
        private bool runtimeDirectoriesInitialized;
        private readonly Dictionary<string, string> assemblyPathsByFullAssemblyName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ignoreRuntimeDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aspnetcorev2_inprocess",
            "clrcompression",
            "clretwrc",
            "clrgc",
            "clrjit",
            "coreclr",
            "D3DCompiler_47_cor3",
            "dbgshim",
            "hostpolicy",
            "Microsoft.DiaSymreader.Native.amd64",
            "Microsoft.DiaSymreader.Native.arm64",
            "Microsoft.DiaSymreader.Native.x76",
            "mscordaccore",
            "mscordbi",
            "mscorrc",
            "mscorrc.debug",
            "msquic",
            "PenImc_cor3",
            "PresentationNative_cor3",
            "System.IO.Compression.Native",
            "ucrtbase",
            "vcruntime140_cor3",
            "wpfgfx_cor3",
        };

        public RepackAssemblyResolver()
        {
            this.ResolveFailure += RepackAssemblyResolver_ResolveFailure;
        }

        private AssemblyDefinition RepackAssemblyResolver_ResolveFailure(object sender, AssemblyNameReference reference)
        {
            InitializeDotnetRuntimeDirectories();

            string fullName = reference.FullName;
            if (assemblyPathsByFullAssemblyName.TryGetValue(fullName, out var filePath))
            {
                var result = ModuleDefinition.ReadModule(filePath).Assembly;
                return result;
            }

            return null;
        }

        public new void RegisterAssembly(AssemblyDefinition assembly)
        {
            base.RegisterAssembly(assembly);
        }

        private void InitializeDotnetRuntimeDirectories()
        {
            if (runtimeDirectoriesInitialized)
            {
                return;
            }

            runtimeDirectoriesInitialized = true;

            var info = new ProcessStartInfo("dotnet", "--list-runtimes");
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;

            using var ps = Process.Start(info);
            var reader = new StringReader(ps.StandardOutput.ReadToEnd());
            ps.WaitForExit();
            if (ps.ExitCode != 0)
            {
                throw new Exception(".NET Core SDK list query failed with code " + ps.ExitCode);
            }

            List<string> allRuntimes = new List<string>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var pathStart = line.LastIndexOf('[') + 1;
                var path = line.Substring(pathStart, line.LastIndexOf(']') - pathStart);
                var runtimeInfo = line.Substring(0, pathStart - 1);
                var parts = runtimeInfo.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var fullPath = Path.Combine(path, parts[1]);
                allRuntimes.Add(fullPath);
            }

            allRuntimes.Reverse();

            ReadRuntimes(allRuntimes);
        }

        private void ReadRuntimes(IEnumerable<string> allRuntimes)
        {
            foreach (var directory in allRuntimes)
            {
                ReadRuntime(directory);
            }
        }

        private void ReadRuntime(string directory)
        {
            var files = Directory.GetFiles(directory, "*.dll");

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (ignoreRuntimeDlls.Contains(fileName) ||
                    fileName.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("mscordaccore_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(filePath);
                    string fullName = assemblyName.FullName;
                    if (!assemblyPathsByFullAssemblyName.ContainsKey(fullName))
                    {
                        assemblyPathsByFullAssemblyName[fullName] = filePath;
                    }
                }
                catch
                {
                }
            }
        }
    }
}
