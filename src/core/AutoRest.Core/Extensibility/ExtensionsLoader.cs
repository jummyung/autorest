// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoRest.Core.Logging;
using AutoRest.Core.Properties;
using AutoRest.Core.Utilities;
using IAnyPlugin = AutoRest.Core.Extensibility.IPlugin<AutoRest.Core.Extensibility.IGeneratorSettings, AutoRest.Core.IModelSerializer<AutoRest.Core.Model.CodeModel>, AutoRest.Core.ITransformer<AutoRest.Core.Model.CodeModel>, AutoRest.Core.CodeGenerator, AutoRest.Core.CodeNamer, AutoRest.Core.Model.CodeModel>;

namespace AutoRest.Core.Extensibility
{
    public static class ExtensionsLoader
    {
        public static IAnyPlugin GetPlugin()
        {
            Logger.Instance.Log(Category.Info, Resources.InitializingCodeGenerator);
            if (Settings.Instance == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (string.IsNullOrEmpty(Settings.Instance.CodeGenerator))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        Resources.ParameterValueIsMissing, "CodeGenerator"));
            }

            IAnyPlugin plugin = null;

            if (Settings.Instance.CodeGenerator.EqualsIgnoreCase("None"))
            {
                plugin = new NoOpPlugin();
            }
            else
            {
                var config = AutoRestConfiguration.Get();
                plugin = LoadTypeFromAssembly<IAnyPlugin>(config.Plugins, Settings.Instance.CodeGenerator);
                Settings.PopulateSettings(plugin.Settings, Settings.Instance.CustomSettings);
            }
            Logger.Instance.Log(Category.Info, Resources.GeneratorInitialized,
                Settings.Instance.CodeGenerator,
                plugin.GetType().GetAssembly().GetName().Version);
            return plugin;

        }

        /// <summary>
        /// Gets the modeler specified in the provided Settings.
        /// </summary>
        /// <param name="settings">The code generation settings</param>
        /// <returns>Modeler specified in Settings.Modeler</returns>
        public static Modeler GetModeler()
        {
            Logger.Instance.Log(Category.Info, Resources.InitializingModeler);
            if (Settings.Instance == null)
            {
                throw new ArgumentNullException("settings", "settings or settings.Modeler cannot be null.");
            }

            if (string.IsNullOrEmpty(Settings.Instance.Modeler))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture,
                        Resources.ParameterValueIsMissing, "Modeler"));
            }

            Modeler modeler = null;

            var config = AutoRestConfiguration.Get();
            modeler = LoadTypeFromAssembly<Modeler>(config.Modelers, Settings.Instance.Modeler);
            Settings.PopulateSettings(modeler, Settings.Instance.CustomSettings);

            Logger.Instance.Log(Category.Info, Resources.ModelerInitialized,
                Settings.Instance.Modeler,
                modeler.GetType().GetAssembly().GetName().Version);
            return modeler;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFrom")]
        public static T LoadTypeFromAssembly<T>(IDictionary<string, AutoRestProviderConfiguration> section,
            string key)
        {
            T instance = default(T);

            if (Settings.Instance != null && section != null && !section.IsNullOrEmpty() && section.ContainsKey(key))
            {
                string fullTypeName = section[key].TypeName;
                if (string.IsNullOrEmpty(fullTypeName))
                {
                    throw ErrorManager.CreateError(Resources.ExtensionNotFound, key);
                }

                int indexOfComma = fullTypeName.IndexOf(',');
                if (indexOfComma < 0)
                {
                    throw ErrorManager.CreateError(Resources.TypeShouldBeAssemblyQualified, fullTypeName);
                }
                string typeName = fullTypeName.Substring(0, indexOfComma).Trim();
                string assemblyName = fullTypeName.Substring(indexOfComma + 1).Trim();

                try
                {
                    Assembly loadedAssembly = null;
                    try
                    {
                        loadedAssembly = Assembly.Load(new AssemblyName(assemblyName));
                    }
                    catch (FileNotFoundException)
                    {
                        // loadedAssembly = Assembly.LoadFrom(assemblyName + ".dll");
                        if (loadedAssembly == null)
                        {
                            throw;
                        }
                    }

                    Type loadedType = loadedAssembly.GetTypes()
                        .Single(t => string.IsNullOrEmpty(typeName) ||
                                     t.Name == typeName ||
                                     t.FullName == typeName);

                    instance = (T)loadedType.GetConstructor(Type.EmptyTypes).Invoke(null);

                    if (!section[key].Settings.IsNullOrEmpty())
                    {
                        foreach (var settingFromFile in section[key].Settings)
                        {
                            Settings.Instance.CustomSettings[settingFromFile.Key] = settingFromFile.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ErrorManager.CreateError(Resources.ErrorLoadingAssembly, key, ex);
                }

                return instance;
            }
            throw ErrorManager.CreateError(Resources.ExtensionNotFound, key);
        }
    }
}
