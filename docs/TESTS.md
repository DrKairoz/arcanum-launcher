# Rapport de validation — 15 octobre 2026

## Effectué dans cet environnement

- SDK .NET **10.0.402**, runtime **10.0.13**, Linux x64.
- Compilation croisée WPF pour **win-x64** réussie, publication **autonome** réussie. Aucun avertissement ni erreur dans le journal de publication.
- Exécutable produit reconnu comme **PE32+ GUI Windows x86-64**. Les DLL du runtime .NET et WPF sont incluses dans la distribution.
- **33 tests C# réussis** dans `tests/Arcanum.Tests`, avec vrais ZIP sur disque et HTTP simulé.
- Tests de publication exécutés avec PowerShell **7.6.7** : ZIP à la racine, liste de fichiers autorisés, empreinte SHA-256, rejet d’un secret, rejet de PCK manquant, mode PCK intégré et analyse syntaxique des scripts.
- XAML et fichiers de projets analysés comme XML ; compilation WPF validant ensuite les ressources, les types et les gestionnaires d’événements.
- Script de compilation/distribution exécuté : génération effective de l’archive Windows.

Les journaux sont dans `docs/validation/`. Les tests ne doivent pas être interprétés comme une recette du véritable jeu.

## Les douze scénarios demandés

| Scénario | Validation réalisée | Reste à faire |
|---|---|---|
| Premier lancement sans jeu | État du moteur sans installation | Premier écran sur Windows |
| Installation complète | HTTP simulé, ZIP, SHA, extraction, version locale | Release GitHub réelle et export Arcanum |
| Lancement Arcanum.exe | Code compilé | Lancement du véritable EXE sur Windows |
| Nouvelle version | Comparaison numérique et analyse API simulée | Publication v0.1.1 réelle |
| Téléchargement et mise à jour | Transaction simulée complète avec conservation des données | Réseau GitHub réel et client Godot |
| Jeu à jour | Comparaison de version testée | Message et bouton dans WPF |
| Hors ligne | Exception HTTP simulée, installation conservée | Cliquer Jouer hors ligne sous Windows |
| Échec/interruption | Téléchargement tronqué et annulation testés | Coupure réseau pendant un gros export |
| Archive/empreinte incorrecte | Rejet testé, ancienne installation intacte | Aucun complément moteur nécessaire |
| Jeu ouvert | Refus testé via détection injectée | Détection du vrai processus Windows |
| Conservation personnelle | Fichier local conservé, collision refusée, fichier géré obsolète supprimé | Sauvegarde Godot réelle avant/après |
| Fermeture/réouverture | Métadonnées persistantes relues et récupération après interruption entre renommages | Cycle complet de fenêtre Windows |

Autres validations : versions bêta rejetées, chemins ZIP dangereux, archive malveillante dans une transaction complète, verrou exclusif, erreur 404/403/429 de GitHub, dossier utilisateur étranger refusé et manque d’espace disque.

## Non exécuté ici

- Affichage, ergonomie, DPI, minimisation et fermeture de la fenêtre WPF sous Windows.
- Installation du raccourci Bureau par COM et copie du launcher sur un PC Windows.
- Véritable release publique : aucun nom de dépôt n’a été fourni et aucune publication n’a été créée.
- Jeu réel, sauvegardes réelles et compatibilité ENet/Playit : aucun export Windows n’a été fourni.
- Exécution du workflow GitHub Actions : le workflow est fourni, mais aucun accès au compte n’a été demandé ou utilisé.
- Injection d’un échec système de renommage et panne électrique réelle : récupération simulée entre les renommages testée ; retour arrière sur exception présent, sans reproduction d’une erreur Windows de verrouillage.

**Statut : build Windows compilée et moteur testé ; recette Windows et jeu réel encore nécessaire avant diffusion large.**

## Reproduire

```powershell
dotnet run --project tests/Arcanum.Tests -c Release
pwsh ./tests/test-publication.ps1
pwsh ./scripts/compiler-launcher.ps1 -Owner MON_COMPTE -Repository arcanum-releases
```

Le workflow Windows exécute les tests, compile, puis lance et ferme la fenêtre pour un premier test de démarrage. Ce test automatisé ne remplace pas la recette visuelle ni le test du jeu réel.
