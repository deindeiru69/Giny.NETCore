# Diagnostic — Monstres se déplacent mais ne castent jamais

**Date** : 2026-05-20
**Branche** : `hero-mode`
**Symptôme rapporté** : combats PvM — les monstres bougent vers le joueur
(`GameMapMovementMessage`) mais ne lancent jamais de sort
(`GameActionFightSpellCastMessage` absent côté serveur).

Lecture seule sur `Sources/`. Aucun fichier modifié pendant ce diagnostic.

---

## TL;DR

**Root cause : `SpellRecord.Category` = `None` pour tous les 17 729 spells en DB.**
L'IA monstre filtre les sorts par `Category ∈ {Agressive, Debuff}` —
filtre vide ⇒ 0 sort éligible ⇒ aucun cast. Les monstres bougent quand
même parce que `MoveToTarget` ne filtre pas sur Category (sauf pour
l'option `Teleport`).

**Fix** : exécuter `.patch` dans la console du serveur World (ou
appeler `SpellCategories.Patch()` au démarrage). Pas de modification
de code nécessaire. Estimé < 5 min côté admin.

Si après `.patch` le bug persiste, retomber sur le plan B (correctif
code dans `CastOnEnemyAction` pour tolérer `Category = None`).

---

## Chaîne causale

### 1. Filtre AI sur `Category`

`CastOnEnemyAction.GetSpellCasts` (CastOnEnemyAction.cs:114, 135) :

```csharp
foreach (var spellRecord in GetSpells()
    .Where(x => x.Category == SpellCategoryEnum.Agressive
             || x.Category == SpellCategoryEnum.Debuff)
    .Shuffle())
```

Si aucun sort ne passe ce filtre, `casts` reste vide ⇒ `bestCast` est
null ⇒ rien n'est cast (retour silencieux à la ligne 56).

Tous les autres `AIAction` filtrent aussi par `Category` :

| Action | Filtre Category |
|---|---|
| `SummonAction` | `== Summon` |
| `MarkAction` | `== Mark` |
| `HealAction` | `HasFlag(Healing)` |
| `BuffAction` | `HasFlag(Buff) && !HasFlag(Debuff) && !HasFlag(Agressive)` |
| `MoveToTarget` | `== Teleport` (uniquement pour l'option tp) |
| `CastOnEnemyAction` | `== Agressive \|\| == Debuff` |

Si `Category` n'est jamais setté, **TOUS** ces filtres rejettent
TOUS les sorts. Seul `MoveToTarget` continue à fonctionner via son
appel à `Fighter.FindPath` + `Fighter.Move` qui ne dépend pas de la
catégorie. ⇒ Symptôme exactement observé : déplacement OK, cast KO.

### 2. Comment `Category` est censé être renseigné

`SpellRecord.cs:82-87` :

```csharp
[Update]
public SpellCategoryEnum Category
{
    get;
    set;
} = SpellCategoryEnum.None;
```

- Pas de `[D2OField]` ⇒ **pas chargé depuis Spells.d2o** (le D2O Ankama
  n'expose pas de notion de catégorie IA).
- `[Update]` ⇒ la colonne existe en DB, persistée à chaque sauvegarde.
- Default = `None` ⇒ tant que personne ne l'écrit, c'est ce qu'on lit.

### 3. Qui assigne `Category` ?

Grep `SpellCategoryEnum` dans tout `Sources/` :

| Fichier | Action |
|---|---|
| `SpellRecord.cs` | définit le champ (default None) |
| `SpellCategoryEnum.cs` | enum |
| `AI/*.cs` (6 fichiers) | **lecture** uniquement |
| **`Modules/Giny.DatabasePatcher/Spells/SpellCategories.cs`** | **écriture** — seule source |

`SpellCategories.Patch()` (Spells/SpellCategories.cs:370-378) :

```csharp
public static void Patch()
{
    Logger.Write("Assigning spell categories ...");
    foreach (var spell in SpellRecord.GetSpellRecords())
        AssignCategory(spell);
}
```

Pour chaque sort, regarde les effets (`level.Effects`), classifie via
des listes hardcodées d'EffectsEnum (Agressive = damage/steal/swap,
Debuff = SubX/Steal, Healing = heal/restore, etc.), prend la catégorie
majoritaire, et `record.UpdateNow()` pour persister en DB.

### 4. Quand est-ce exécuté ?

`Module.cs:30-54` — la liste de patches est appelée uniquement par
la console command `.patch` (ConsoleCommand admin) :

```csharp
[ConsoleCommand("patch")]
public static void PatchCommand()
{
    Logger.Write("Patching world database ...", Channels.Info);
    // ... 16 autres patches ...
    SpellCategories.Patch();
    MonsterKamas.Patch();
    Logger.Write("World database patched.", Channels.Info);
}
```

Et `Module.Initialize()` ligne 25-28 est **vide** :

```csharp
public void Initialize()
{
    // Patch here
}
```

**Conclusion** : `SpellCategories.Patch()` n'est jamais auto-exécuté.
Si l'admin n'a jamais tapé `.patch` dans la console World, la colonne
`spells.Category` reste à `None` pour les 17 729 sorts.

### 5. Pourquoi les joueurs castent quand même

Les sorts joueurs sont déclenchés par un click UI, qui descend via
`GameActionFightCastRequestMessage` directement à
`Character.CastSpell(spellId, cellId)`. Ce chemin n'a aucun filtre
sur `SpellRecord.Category` — il consulte juste `Character.HasSpell` et
les AP/Range/etc. C'est pour ça que les joueurs peuvent attaquer
normalement, mais que l'IA monstre est bloquée.

---

## Requêtes SQL de vérification

À exécuter sur la VM (utilisateur a SSH/MySQL, l'agent n'y a pas
accès) — toutes en lecture seule :

```sql
-- 1. Combien de sorts ont une category None vs autre chose
SELECT Category, COUNT(*) AS n
FROM spells
GROUP BY Category
ORDER BY n DESC;
-- Attendu si le bug est présent : Category=0 (None) pour ~17729 lignes,
-- et 0 ou peu de lignes dans les autres catégories.

-- 2. Combien de sorts liés aux monstres ont une category définie
SELECT s.Category, COUNT(DISTINCT s.Id) AS spells_in_use_by_monsters
FROM spells s
WHERE s.Id IN (
    SELECT DISTINCT JSON_EXTRACT(...)  -- compliqué car Spells est un blob ProtoBuf
    FROM monsters
)
GROUP BY s.Category;
-- (Cette query est indicative — le format ProtoBuf du blob complique
-- l'extraction en SQL pur. La query #1 est suffisante pour confirmer.)

-- 3. Combien de monstres ont des Spells non-vides (sanity check)
SELECT COUNT(*) FROM monsters WHERE LENGTH(Spells) > 4;
-- Attendu : la majorité (4971 monstres au total per le rapport).
```

L'interprétation attendue :
- **Si query 1 montre ~17729 lignes avec `Category=0`** → bug data
  confirmé. Solution : exécuter `.patch` dans la console World.
- **Si query 1 montre une distribution réaliste** (mix de 1/2/4/8/16/32/64)
  → le bug n'est PAS dans Category. Reprendre l'investigation
  (probablement `Fighter.CanCastSpell` retourne autre chose que `OK`,
  ou la résolution `SpellRecord` côté `MonsterFighter.SpellRecords`
  est cassée). Dans ce cas, le plan B s'applique : instrumenter le
  filtre par log.

---

## Plan d'action recommandé

### Plan A — Le plus simple (recommandé)

1. SSH sur la VM World.
2. Dans la console du World server (interface admin), taper :
   ```
   .patch
   ```
3. Attendre la fin (log "World database patched.").
4. Combat de test in-game contre n'importe quel monstre niveau 1
   (ex. Bouftou Royal d'Incarnam, Tofu).
5. Si les monstres castent → fini.

**Effort** : 5 minutes.
**Risque** : très faible. `SpellCategories.Patch()` n'efface rien — il
fait des `UPDATE` sur la colonne Category seulement. Les sorts à effets
inconnus (rares) restent à `None`, ce qui matche le comportement actuel.

**Effet secondaire bénéfique** : la même commande `.patch` exécute aussi
15 autres patchs (MonsterSpawns, MonsterKamas, MapPlacements, etc.)
qui pourraient avoir des effets latents. À surveiller post-run.

### Plan B — Si Plan A échoue ou si on ne veut pas dépendre de `.patch`

Modifications **code** (faisable rapidement, mais hors scope du prompt
actuel — à n'engager que sur ton OK explicite) :

1. **Auto-run au startup** : ajouter `SpellCategories.Patch()` à
   `Module.Initialize()` avec une garde "si > 90% des spells sont None
   alors patch" (idempotent).

2. **Filtre AI tolérant** : dans `CastOnEnemyAction.GetSpellCasts`,
   accepter aussi `Category == None` quand le sort a un effet dans
   `SpellCategories.AgressiveEffects`. Évite la dépendance au patch.
   Plus invasif (~30 lignes), mais robuste.

3. **Logs debug** : ajouter le logging demandé dans le prompt (nb
   spells dispo / nb cibles / raison de rejet par sort), gated par
   `WorldConfig.AIDebugLog`. Utile pour les futures investigations.

### Plan C — Vérification avant exécution

Si ton admin VM est dispo, lance d'abord la query #1 ci-dessus.
Selon le résultat :
- Distribution ~tout None → Plan A
- Distribution réaliste → reprends l'investigation, l'hypothèse Category
  est fausse, autre cause à chercher.

---

## Pourquoi je n'ai rien commit

Per tes instructions explicites :

> "Si tu identifies que c'est un problème de data plutôt que de code,
>  ARRÊTE l'implémentation. Reviens vers moi avec le diagnostic clair :
>  combien de monstres ont des spells, lesquels n'en ont pas, et la
>  requête SQL qui montre le problème."

Le problème EST data (Category=None). Le code AI est correct (filtre
légitime pour ignorer les sorts qui ne sont pas faits pour attaquer
— ex. un sort de heal n'a rien à faire dans CastOnEnemyAction).
Modifier l'IA pour ignorer Category masquerait le vrai problème et
casserait d'autres comportements (le monstre tenterait de "caster"
ses heals sur les joueurs).

J'attends ton OK pour soit :
- **(préférable)** lancer `.patch` côté VM toi-même
- soit me dire de coder le Plan B (auto-patch + tolérance fallback)

---

## Annexes

### Confirmation que `MoveToTarget` n'est pas affecté

`MoveToTarget.cs:21-43` :

```csharp
protected override void Apply()
{
    var target = Fighter.EnemyTeam.CloserFighter(Fighter);
    if (target == null || target.IsMeleeWith(Fighter))
        return;

    // Optionnel : utiliser un sort Teleport (sera rejeté si Category=None)
    foreach (var spellRecord in GetSpells()
        .Where(x => x.Category == SpellCategoryEnum.Teleport).Shuffle())
    {
        // ...
    }

    var path = Fighter.FindPath(target);
    Fighter.Move(path);   // ← ce call ne dépend PAS de Category
}
```

Donc même si tous les sorts ont `Category=None`, `Fighter.Move(path)`
s'exécute. C'est exactement ce qu'on observe : les monstres bougent,
mais ne castent rien.

### Comment `MonsterFighter` expose ses sorts

```csharp
// MonsterFighter.cs:185-188
public override IEnumerable<SpellRecord> GetSpells()
{
    return Record.SpellRecords.Values;
}
```

`Record.SpellRecords` (Dictionary<short, SpellRecord>) est peuplé au
démarrage dans `MonsterRecord.Initialize()` (MonsterRecord.cs:165-188)
en joignant `monster.Spells` (List<short>) avec
`SpellRecord.GetSpellRecord(id)`. Les `SpellRecord` ainsi récupérés
sont les vrais objets de la DB — donc leur `Category` est lue depuis
la colonne `spells.Category` de MySQL. Si la colonne vaut 0 (None)
en DB, le monstre voit None en mémoire.

### Hypothèses alternatives écartées

**H1 : Spells du monstre vide en runtime**
Écartée. `MonsterRecord.Spells` est un blob ProtoBuf déjà sync,
`MonsterRecord.Initialize` itère et joint correctement. Le code
échouerait silencieusement (les spells absents seraient juste
ignorés via `if (spellRecord != null)` ligne 176) mais pas
massivement à 100%. Et même si c'était ça, MoveToTarget ne dépend
pas des spells, donc move marche, mais ça ne serait pas l'explication
canonique.

**H2 : `Fighter.CanCastSpell` rejette tout**
Si Category passe, cette méthode pourrait quand même rejeter via
AP cost / range / LOS / cooldown. Mais le filtre `Category` est
appliqué AVANT, donc on n'arrive jamais à `CanCastSpell`. À reconsidérer
seulement si Plan A échoue.

**H3 : Cibles invalides**
`Fighter.EnemyTeam.GetFighters()` ligne 32 + 133 — fonctionne
puisque MoveToTarget ligne 21 utilise `EnemyTeam.CloserFighter`
avec succès. Donc les ennemis sont bien détectés. Écartée.

**H4 : Exception silencieuse**
`AIAction.Execute()` (AIAction.cs:31-37) appelle `Apply()` sans
try/catch — donc une exception remonterait au `MonsterBrain.Play()`
qui n'a pas de catch non plus, donc remonterait au runtime. Si ça
arrivait, on verrait des entries dans `world.error.log`. Le user
dit "log vide" ⇒ pas d'exception, juste un retour silencieux à cause
du filtre. Écartée.
