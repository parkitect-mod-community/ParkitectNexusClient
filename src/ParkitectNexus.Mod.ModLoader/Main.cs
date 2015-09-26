﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MiniJSON;
using UnityEngine;
using Path = System.IO.Path;

namespace ParkitectNexus.Mod.ModLoader
{
    public class TempFix : MonoBehaviour
    {
        private bool isloaded;

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void Update()
        {
            if (Application.loadedLevel == 2 && !isloaded)
            {
                isloaded = true;
                ModManager.Instance.triggerEnable();
            }
        }
    }

    public static class Main
    {
        private static GameObject go;

        public static void Load()
        {
            go = new GameObject();
            go.AddComponent<TempFix>();

            var gamePath = System.IO.Path.Combine(Application.dataPath, "../");
            var modsPath = System.IO.Path.Combine(gamePath, "mods");
            
            foreach (var folder in Directory.GetDirectories(modsPath))
            {
                try
                {
                    var filePath = System.IO.Path.Combine(folder, "mod.json");
                    
                    if (!File.Exists(filePath))
                        continue;
                    
                    var dictionary = Json.Deserialize(File.ReadAllText(filePath)) as Dictionary<string, object>;

                    object isEnabled, isDevelopment, nameSpace, className;
                    
                    dictionary.TryGetValue("IsEnabled", out isEnabled);
                    dictionary.TryGetValue("IsDevelopment", out isDevelopment);
                    dictionary.TryGetValue("NameSpace", out nameSpace);
                    dictionary.TryGetValue("ClassName", out className);
                    
                    bool bIsEnabled = (isEnabled is bool) ? (bool) isEnabled : false,
                        bIsDevlopment = (isDevelopment is bool) ? (bool) isDevelopment : false;
                    string sNameSpace = (nameSpace is string) ? (string) nameSpace : null,
                        sClassName = (className is string) ? (string) className : null;
                    
                    if (!bIsDevlopment && !bIsEnabled) continue;

                    var compiledPath = System.IO.Path.Combine(folder, "compiled.dll");
                    if (!File.Exists(compiledPath)) continue;
                    
                    var assembly = Assembly.LoadFile(compiledPath);
                    var userMod = assembly.CreateInstance(sNameSpace + '.' + sClassName) as IMod;
                    if (userMod == null) continue;
                    ModManager.Instance.addMod(userMod);
                }
                catch (Exception e)
                {
                    File.AppendAllText(System.IO.Path.Combine(folder, "mod.log"), "LOAD ERROR: " + e.Message + "\n");
                }
            }
        }
    }
}