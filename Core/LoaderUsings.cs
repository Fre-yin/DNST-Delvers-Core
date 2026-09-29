// Game namespaces and loader-dependent aliases for every file of this assembly.
// MelonLoader's interop prefixes the game's namespaces with Il2Cpp; BepInEx keeps the original names.
#if BEPINEX
global using global::Refactor;
global using global::Refactor.Component;
global using global::Refactor.Main;
global using global::Refactor.Main.Event;
global using global::Refactor.Map;
global using global::Refactor.Setting;
global using global::Refactor.UI;
global using global::Refactor.Util;
global using global::Refactor.View;
global using TMPro;
global using global::Util.Sheet;
global using ComponentSaveList = Il2CppSystem.Collections.Generic.List<global::Refactor.ComponentSaveData>;
global using GameFileLogger = global::FileLogger;
global using HolderList = Il2CppSystem.Collections.Generic.List<global::Refactor.Component.AffecterHolder>;
global using Il2CppCandidates = Il2CppSystem.Collections.Generic.List<global::Refactor.RecruitCandidateData>;
#else
global using Il2CppRefactor;
global using Il2CppRefactor.Component;
global using Il2CppRefactor.Main;
global using Il2CppRefactor.Main.Event;
global using Il2CppRefactor.Map;
global using Il2CppRefactor.Setting;
global using Il2CppRefactor.UI;
global using Il2CppRefactor.Util;
global using Il2CppRefactor.View;
global using Il2CppTMPro;
global using Il2CppUtil.Sheet;
global using ComponentSaveList = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.ComponentSaveData>;
global using GameFileLogger = global::Il2Cpp.FileLogger;
global using HolderList = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.Component.AffecterHolder>;
global using Il2CppCandidates = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.RecruitCandidateData>;
#endif
