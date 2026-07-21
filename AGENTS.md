# Instructions pour les Agents IA

## Technologies et Frameworks

### Framework Principal
- **Avalonia UI** : Version 12.1.0
  - Framework UI cross-platform pour .NET
  - Utilisation des bindings compilés par défaut (`AvaloniaUseCompiledBindingsByDefault`)
  - Thème Fluent (Avalonia.Themes.Fluent 12.1.0)
  - Police Inter (Avalonia.Fonts.Inter 12.1.0)

### Plateforme .NET
- **Target Framework** : .NET 10.0 (net10.0-windows10.0.26100.0)
- **Platform Target** : x64
- **Runtime Identifier** : win-x64
- **Self-Contained** : false
- **Nullable** : enabled
- **ImplicitUsings** : enabled
- **AllowUnsafeBlocks** : true

### Bibliothèques Tierces
- **LibVLCSharp** : Version 3.10.0
  - Wrapper .NET pour LibVLC
- **VideoLAN.LibVLC.Windows** : Version 3.0.23.1
  - Bibliothèque native VLC pour Windows
- **FluentIcons.Avalonia.Fluent** : Version 2.1.331
  - Icônes Fluent pour Avalonia

### Outils de Développement
- **Avalonia.Diagnostics** : Version 11.3.18
  - Outils de débogage et d'inspection UI pour Avalonia

## Règles de Qualité de Code

Le code doit respecter les **standards de qualité d'un projet C# professionnel** :

### Conventions de Nommage
- **PascalCase** pour les classes, méthodes, propriétés publiques
- **camelCase** pour les variables locales et paramètres
- **_camelCase** pour les champs privés (avec underscore)
- Noms significatifs et descriptifs

### Bonnes Pratiques
- Utiliser les nullable reference types correctement
- Respecter les principes SOLID
- Éviter la duplication de code (DRY)
- Commenter uniquement quand nécessaire (code auto-documenté privilégié)
- TOUT les commentaires dans le code doivent être en ANGLAIS
- Gestion appropriée des erreurs et exceptions
- Dispose pattern pour les ressources non managées
- Async/await pour les opérations asynchrones

### Organisation du Code
- Une classe par fichier (sauf classes imbriquées)
- Grouper les membres par type (propriétés, constructeurs, méthodes)
- Respecter les modificateurs d'accès appropriés

## Préférences de Documentation

**IMPORTANT** : Ne pas générer de documentation Markdown automatiquement lors de la création de plans ou de modifications de code.

- ❌ Pas de génération automatique de documentation
- ❌ Pas de fichiers README ou CHANGELOG non sollicités
- ✅ Documentation générée **uniquement sur demande explicite**
- ✅ Commentaires dans le code si nécessaire pour la clarté

## Architecture du Projet

- **Type de projet** : Application Windows avec Avalonia UI
- **Architecture cible** : x64 uniquement
- **Système d'exploitation** : Windows 10.0.26100.0+
- **Point d'entrée** : `DMVideoPlayer.Program`

## Notes Spécifiques

### LibVLC
- Le projet exclut automatiquement les binaires win-x86 et win-arm64 non utilisés
- Nettoyage post-build des dossiers `libvlc\win-x86` et `libvlc\win-arm64`
- Configuration optimisée pour x64 uniquement
