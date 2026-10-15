# Publier Arcanum, pas à pas

Deux dépôts sont conseillés : un pour **les sources du launcher**, public ou privé, et un dépôt **public pour les distributions du jeu**. Le projet Godot complet et le serveur peuvent rester ailleurs.

## 1. Créer le dépôt public du jeu

Sur GitHub, créez un nouveau dépôt nommé par exemple `arcanum-releases`. Choisissez **Public** et ajoutez un README. N’envoyez pas votre projet Godot complet. Notez les deux valeurs : propriétaire (votre identifiant GitHub) et dépôt (`arcanum-releases`).

## 2. Créer et compiler le launcher

Créez un autre dépôt, par exemple `arcanum-launcher`. Importez **le contenu du dossier ArcanumLauncher de cette archive à la racine** : vous devez voir `src`, `scripts`, `tests`, `docs` et `.github` directement dans le dépôt. GitHub Desktop peut faciliter l’importation du dossier, y compris `.github`.

Le fichier `.github/workflows/build-launcher.yml` doit être présent sur la branche par défaut. Ouvrez **Actions**, puis **Compiler et tester Arcanum Launcher**, puis **Run workflow**. Renseignez le propriétaire et le nom du dépôt public du jeu.

Le workflow installe le SDK, lance les tests, compile WPF en distribution autonome Windows x64, teste le démarrage de la fenêtre puis prépare l’archive. N’utilisez l’artefact qu’après succès de toutes les étapes. En cas d’erreur, ouvrez l’étape rouge et transmettez son journal.

Dans la page du run réussi, téléchargez l’artefact **ArcanumLauncher-Windows-x64**. GitHub enveloppe l’archive distribuable dans son propre ZIP : extrayez-le pour retrouver `ArcanumLauncher-Windows-x64.zip`. C’est cette archive intérieure qu’il faut donner aux testeurs.

Alternative locale, avec Windows, SDK .NET 10 et PowerShell 7 :

```powershell
pwsh ./scripts/compiler-launcher.ps1 -Owner MON_IDENTIFIANT -Repository arcanum-releases
```

Les testeurs n’ont besoin d’aucun SDK ni de PowerShell 7 : la distribution inclut le runtime .NET et un installateur compatible avec le PowerShell Windows intégré.

## 3. Exporter le jeu depuis Godot

Ouvrez votre projet dans Godot et utilisez votre préréglage **Windows Desktop** pour exporter le client dans un dossier vide, par exemple `C:\Arcanum-Export`. N’exportez pas le serveur dédié. Nommez le client `Arcanum.exe`.

Sans PCK intégré, vous devez obtenir `Arcanum.exe` et `Arcanum.pck`. Avec l’option PCK intégré, le PCK est inclus dans l’exécutable : passez `-EmbeddedPck` au script suivant. Testez l’EXE directement avant de publier.

Vérifiez les filtres d’export Godot : aucun fichier de sauvegarde, jeton, script d’administration ni secret ne doit se retrouver dans le PCK. Le script de préparation ne peut pas vérifier l’intérieur du PCK.

## 4. Préparer v0.1.0

Dans PowerShell 7 à la racine des sources du launcher :

```powershell
pwsh ./scripts/preparer-release.ps1 -ExportDirectory 'C:\Arcanum-Export' -Version v0.1.0 -OutputDirectory 'C:\Releases\v0.1.0'
```

Avec PCK intégré : ajoutez `-EmbeddedPck`.

Si l’export a besoin d’une DLL ou d’un autre fichier supplémentaire, ajoutez explicitement les chemins relatifs :

```powershell
pwsh ./scripts/preparer-release.ps1 -ExportDirectory 'C:\Arcanum-Export' -Version v0.1.0 -OutputDirectory 'C:\Releases\v0.1.0' -ExtraFiles @('une-extension.dll','data/ressource.bin')
```

Seuls les fichiers explicitement sélectionnés sont copiés : EXE, PCK si externe, puis ExtraFiles. Ne sélectionnez jamais un fichier personnel ou serveur. Utilisez un **nouveau** dossier de sortie à chaque préparation.

Résultat : `Arcanum-Windows.zip`, `Arcanum-Windows.zip.sha256`, `NOTES-RELEASE.md`. L’EXE doit être à la racine du ZIP, sans dossier parent.

## 5. Publier la première release

Dans `arcanum-releases`, ouvrez **Releases**, puis **Draft a new release** (ou Create a new release).

- Créez le tag `v0.1.0` sur la branche par défaut.
- Titre : `Arcanum v0.1.0`.
- Collez les notes adaptées à partir de NOTES-RELEASE.md.
- Ajoutez **Arcanum-Windows.zip** et **Arcanum-Windows.zip.sha256** dans les pièces jointes.
- Ne cochez pas préversion. La release doit être stable et marquée comme dernière publication si l’option est proposée.
- Cliquez sur **Publish release**.

Les archives Source code ajoutées automatiquement par GitHub ne sont pas utilisées par le launcher. N’intégrez aucun token GitHub dans les fichiers ou le launcher.

## 6. Tester sur le deuxième PC

Suivez INSTALLATION-TESTEURS.md. Si nécessaire, renseignez le dépôt dans Paramètres. Vérifiez : installation, notes, version affichée, lancement réel, solo, sauvegarde puis fermeture et réouverture. Testez aussi hors ligne après installation.

Ce test sur le jeu réel est indispensable : les tests automatisés utilisent des ZIP factices pour vérifier le moteur de mise à jour.

## 7. Publier v0.1.1

Exportez le client mis à jour dans un dossier vide. Relancez le script avec `-Version v0.1.1` et un nouveau dossier de sortie. Créez une nouvelle release stable `v0.1.1`, ajoutez son ZIP et son SHA-256, publiez-la comme dernière version.

Fermez Arcanum sur le deuxième PC, relancez le launcher avec vérification automatique activée. Il doit télécharger la nouvelle version, installer puis afficher v0.1.1. Vérifiez que la sauvegarde précédente du joueur est intacte. Sinon arrêtez la diffusion et conservez les journaux pour diagnostic.

N’utilisez pas les numéros des patches internes Godot comme tags sans décision explicite : ils sont indépendants. Ne remplacez pas une ancienne release pour modifier silencieusement son contenu ; publiez un nouveau numéro.

## 8. Mettre à jour le launcher indépendamment

Recompilez ses sources avec le workflow. Distribuez la nouvelle archive du launcher et suivez le remplacement manuel décrit dans INSTALLATION-TESTEURS.md. Les releases du jeu ne remplacent jamais le launcher.

## Références officielles

- API Releases : https://docs.github.com/en/rest/releases/releases
- Publication .NET autonome : https://learn.microsoft.com/dotnet/core/deploying/
- GitHub Actions, artefacts : https://docs.github.com/actions/managing-workflow-runs/downloading-workflow-artifacts

L’interface GitHub peut évoluer ; les éléments essentiels restent le tag stable, les deux assets exacts et le statut latest.
