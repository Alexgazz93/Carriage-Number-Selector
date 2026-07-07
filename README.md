# TrainCarSelector

Mod for Cities: Skylines that lets you add, remove, replace, and reorganize wagons for trains, metro, monorail, and tram vehicles directly from the vehicle info panel.

## English

### Features
- Add or remove wagons from the selected vehicle.
- Replace a selected wagon with another compatible wagon from the list.
- Apply the exact composition of one train to other trains on the same line.
- Reverse the selected wagon.
- Show a side list of compatible wagons.
- Preserve the original train layout for reset.
- Reduce orphan wagons and selection/follow issues during composition changes.

### Project structure
- `TrainCarSelector/Class1.cs` — main source file for UI and vehicle logic.

### Build
1. Open the solution in Visual Studio 2026.
2. Target framework: .NET Framework 4.7.2, C# 7.3.
3. Build the solution. The DLL is generated in `bin\Debug` or `bin\Release`.

### Local installation for testing
Copy the compiled DLL to the Cities: Skylines Mods folder:
- `%UserProfile%\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\TrainCarSelector\`

Create the `TrainCarSelector` folder and place the DLL there. Add assets if required.

### Contributing
- Open a pull request on GitHub.
- Describe your changes and include a short changelog.
- Do not commit `bin/` folders or binary build outputs.

### License
Add a `LICENSE` file if you want to share the project publicly. MIT is recommended.

## Français

### Fonctionnalités
- Ajouter ou retirer des wagons au véhicule sélectionné.
- Remplacer un wagon sélectionné par un autre wagon compatible depuis la liste.
- Appliquer la composition exacte d’un train à d’autres trains de la même ligne.
- Inverser le wagon sélectionné.
- Afficher une liste latérale des wagons compatibles.
- Conserver la composition d’origine du train pour la réinitialisation.
- Réduire les wagons orphelins et les problèmes de sélection/suivi lors des modifications.

### Structure du projet
- `TrainCarSelector/Class1.cs` — fichier principal contenant l’interface utilisateur et la logique des véhicules.

### Compilation
1. Ouvrir la solution dans Visual Studio 2026.
2. Framework cible : .NET Framework 4.7.2, C# 7.3.
3. Compiler la solution. La DLL est générée dans `bin\Debug` ou `bin\Release`.

### Installation locale pour test
Copier la DLL compilée dans le dossier Mods de Cities: Skylines :
- `%UserProfile%\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\TrainCarSelector\`

Créer le dossier `TrainCarSelector` et y placer la DLL. Ajouter les assets si nécessaire.

### Contribution
- Ouvrir une pull request sur GitHub.
- Décrire les changements et ajouter un court changelog.
- Ne pas committer les dossiers `bin/` ni les artefacts binaires.

### Licence
Ajouter un fichier `LICENSE` si vous souhaitez partager le projet publiquement. MIT est recommandé.
