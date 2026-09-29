// The offline tests compile a few Core files against the doubles in PackLoadIntegrationTestDoubles.cs.
// These global usings stand in for Core/LoaderUsings.cs, whose other game namespaces the doubles do not provide.
#if BEPINEX
global using global::Refactor;
global using global::Refactor.Component;
global using global::Refactor.Main;
global using ComponentSaveList = Il2CppSystem.Collections.Generic.List<global::Refactor.ComponentSaveData>;
#else
global using Il2CppRefactor;
global using Il2CppRefactor.Component;
global using Il2CppRefactor.Main;
global using ComponentSaveList = Il2CppSystem.Collections.Generic.List<Il2CppRefactor.ComponentSaveData>;
#endif
