using System.Reflection;
using System.Resources;

// Neutrale Ressourcen (UITexte.resx, Resources.resx) sind deutsch; Englisch liegt als
// Satellit (UITexte.en.resx) daneben. Damit sucht der ResourceManager fuer "de" nicht
// erst nach einem Satelliten, der nicht existiert (CA1824).
[assembly: NeutralResourcesLanguage("de")]

// Removed duplicate AssemblyTitle attribute to fix CS0579 error
//[assembly: AssemblyTitle("MortysDLP")]
//[assembly: AssemblyProduct("MortysDLP")]
//[assembly: AssemblyCompany("MortysDLP")]
//[assembly: AssemblyCopyright("Copyright � 2025")]