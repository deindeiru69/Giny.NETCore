# IncarnamScan

Scanner D2O one-shot pour produire l'inventaire de la zone Incarnam :
SubAreas, MapIds, NPCs (TemplateIds + métadonnées), Quests (steps + objectives).

Conçu pour alimenter `docs/incarnam-inventory.md` et identifier les
handlers `QuestObjectiveTypeEnum` qu'il reste à implémenter côté serveur
pour rendre les quêtes Incarnam jouables de bout en bout.

## Build + run

```bash
cd tools/IncarnamScan
dotnet run                              # client à <repo>/Ressources/Dofus/
dotnet run -- "D:\Path\to\Dofus"        # client custom
```

Le scanner :

1. Initialise `D2OManager` sur `<ClientPath>/data/common/` et `D2IManager` sur
   `<ClientPath>/data/i18n/`.
2. Filtre `SubAreas.d2o` sur le nom i18n contenant "Incarnam" (case-insensitive).
3. Extrait les `MapIds` (via `SubArea.MapIds` + `MapPositions.d2o` pour les
   coordonnées posX/posY).
4. Extrait les NPCs référencés par les SubAreas Incarnam (`SubArea.Npcs` →
   premier élément de chaque sous-liste = TemplateId, cross-référencé avec
   `Npcs.d2o` pour le nom + look).
5. Extrait les quêtes référencées (`SubArea.Quests`), résout chaque step et
   chaque objective avec son type, ses paramètres et son MapId.
6. Génère un récap des occurrences par `QuestObjectiveTypeEnum` avec un
   marqueur "handler implémenté ?" basé sur ce que sait faire `QuestManager`
   côté Giny.World (à ce jour : `GoToNpc` (1) et `NpcTalkBack` (9)).

## Fichiers de sortie (output/)

- `subareas.json` — la ou les SubArea Incarnam avec Id, AreaId, Name, MapCount,
  NpcEntries, QuestEntries.
- `maps.json` — toutes les maps des SubAreas Incarnam : MapId, PosX/PosY,
  Outdoor, IsTransition, SubAreaName.
- `npcs.json` — NPCs templates : TemplateId, Name, Look (string Ankama),
  DialogMessagesCount, ActionsAuthorized (enum NpcActionsEnum byte values).
- `quests.json` — pour chaque quête : QuestId, Name, LevelMin/Max, CategoryId,
  StartCriterion, Followable, et la liste imbriquée Steps → Objectives
  (TypeId, TypeName, Params, MapId, HandlerImplemented).
- `summary.json` — agrégation : compteurs SubArea/Map/NPC/Quest et la
  distribution des objective types avec leur état d'implémentation côté Giny.

Ces fichiers sont déterministes (ordonnés par Id) et destinés à être commités
pour pouvoir tracker les évolutions du contenu Incarnam entre versions Dofus.

## Caveats

- **NPC placement précis sur les maps** : non extrait. Les `SubArea.Npcs`
  donnent l'ensemble des NPCs de la sous-zone mais pas leur cellule. Pour
  un placement fidèle à Ankama il faut parser les `.dlm` (Maps.d2p), pas
  couvert par ce scanner.
- **Quêtes hors `SubArea.Quests`** : certaines quêtes peuvent ne pas être
  référencées dans le D2O de la SubArea où elles se déroulent (système de
  quêtes-meta, secondaires globales). Le scanner ignore ces quêtes. Pour
  un audit exhaustif, faire un second pass filtrant `Quests.d2o` sur
  `levelMin <= 10` et regarder leur premier objective.
- **Handler implementation status** : la liste `ImplementedObjectiveTypes`
  dans `Program.cs` est à jour pour l'état du repo au moment de l'écriture.
  Si tu ajoutes un handler à `QuestManager`, mets à jour aussi cette liste
  pour que le `HandlerImplemented` reflète la réalité.

## Dependencies

- `Sources/Giny.Core/` (Json helpers, Logger, etc.)
- `Sources/Giny.IO/` (D2OManager, D2IManager, D2OClasses, Newtonsoft.Json
  transitivement)

Pas de dépendance à `Giny.World` (pas de DB, pas d'ORM). Build standalone.

## Workflow de rafraîchissement

Quand le client Dofus est mis à jour (`Ressources/Dofus/data/common/` ou
`data/i18n/` changent) :

```bash
cd tools/IncarnamScan
dotnet run
git diff output/                        # voir le delta
```

Et reflèter les changements significatifs dans `docs/incarnam-inventory.md`.
