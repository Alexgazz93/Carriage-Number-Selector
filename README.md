# TrainCarSelector

Carriage Number Selector — mod Cities: Skylines pour ajouter/retirer des wagons (train, metro, monorail, tram) depuis la fenêtre d'info véhicule.

## Structure
- `TrainCarSelector/Class1.cs` — code source principal (UI + logique véhicules).

## Compilation
1. Ouvrir la solution dans Visual Studio 2026.
2. Cible : .NET Framework 4.7.2, C# 7.3.
3. Build → `Build Solution`. Le fichier DLL est généré dans `bin\Debug` ou `bin\Release`.

## Installation locale (test)
Copier la DLL compilée dans le dossier Mods de Cities: Skylines :
- `%UserProfile%\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\TrainCarSelector\`  
Créer le dossier `TrainCarSelector` et y déposer la DLL et, si nécessaire, les assets.

## Contribution
- Ouvrir un PR sur GitHub.
- Expliquer les changements, ajouter un court changelog.
- Ne pas committer les dossiers `bin/` ni fichiers binaires.

## Licence
Ajoutez un `LICENSE` (MIT recommandé) si vous souhaitez partager librement.