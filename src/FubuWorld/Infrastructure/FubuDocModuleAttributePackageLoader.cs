﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bottles;
using Bottles.Diagnostics;
using Bottles.PackageLoaders.Assemblies;
using FubuCore;
using FubuDocs;
using FubuMVC.Core;
using FubuMVC.Core.Packaging;

namespace FubuWorld.Infrastructure
{
    public class FubuDocModuleAttributePackageLoader : IPackageLoader
    {
        public IEnumerable<IPackageInfo> Load(IPackageLog log)
        {
            var list = new List<string> { AppDomain.CurrentDomain.SetupInformation.ApplicationBase };

            string binPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath;
            if (binPath.IsNotEmpty())
            {
                if (Path.IsPathRooted(binPath))
                {
                    list.Add(binPath);
                }
                else
                {
                    list.Add(AppDomain.CurrentDomain.SetupInformation.ApplicationBase.AppendPath(binPath));
                }
            }



            // This is a workaround for Self Hosted apps where the physical path is different than the AppDomain's original
            // path
            if (FubuMvcPackageFacility.PhysicalRootPath.IsNotEmpty())
            {

                var path = FubuMvcPackageFacility.PhysicalRootPath.ToFullPath().AppendPath("bin");
                if (Directory.Exists(path) && !list.Select(x => x.ToLower()).Contains(path.ToLower()))
                {
                    list.Add(path);
                }
            }

            list.Each(x =>
            {
                log.Trace("Looking for assemblies marked with the [FubuDocModule] attribute in " + x);
            });

            return LoadPackages(list);
        }

        public IEnumerable<IPackageInfo> LoadPackages(List<string> list)
        {
            IEnumerable<Assembly> assemblies = FindAssemblies(list).Where(assem => assem.GetName().Name.EqualsIgnoreCase(IgnoreAssembly));
            return assemblies
                .Select(assem => new AssemblyPackageInfo(assem));
        }

        public static IEnumerable<Assembly> FindAssemblies(IEnumerable<string> directories)
        {
            return directories.SelectMany(
                x =>
                AssembliesFromPath(x, assem => assem.GetCustomAttributes(typeof(FubuDocModuleAttribute), false).Any()));
        }

        // TODO -- this is so common here and in FubuMVC, just get something into FubuCore
        public static IEnumerable<Assembly> AssembliesFromPath(string path, Predicate<Assembly> assemblyFilter)
        {


            var assemblyPaths = Directory.GetFiles(path)
                .Where(file =>
                       Path.GetExtension(file).Equals(
                           ".exe",
                           StringComparison.OrdinalIgnoreCase)
                       ||
                       Path.GetExtension(file).Equals(
                           ".dll",
                           StringComparison.OrdinalIgnoreCase));

            foreach (string assemblyPath in assemblyPaths)
            {
                Assembly assembly =
                    AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(
                        x => x.GetName().Name == Path.GetFileNameWithoutExtension(assemblyPath));

                if (assembly == null)
                {
                    try
                    {
                        assembly = Assembly.LoadFrom(assemblyPath);
                    }
                    catch
                    {
                    }
                }




                if (assembly != null && assemblyFilter(assembly))
                {
                    yield return assembly;
                }
            }
        }

        public string IgnoreAssembly { get; set; }
    }
}