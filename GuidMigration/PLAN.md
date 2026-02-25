# Couchbase GUID Migration -- Implementation Plan

## Context

Migrate Classification, SubClassification, and Hierarchy collections from source Couchbase (`PRODDATA`) to target (`Daily-Backup`) for `companyId = 1`, scope `UCG`. Generate **new GUIDs** for all documents while preserving parent-child relationships. Set `microlearning`, `jobbing`, `tracebility` arrays to `null`.

- **Source:** `couchbase://162.19.239.150`, user: `ucgdev`, bucket: `PRODDATA`
- **Target:** `couchbase://localhost`, user: `Administrator`, bucket: `Daily-Backup`
- **Scope:** `UCG`
- **Filter:** `companyId = 1`

---

## Project Structure

```text
GuidMigration/
  GuidMigration.sln
  PLAN.md                             <-- This file
  GuidMigration/
    GuidMigration.csproj
    Program.cs                        -- Entry point
    appsettings.json                  -- Source/Target connection config
    Models/
      Classification.cs               -- Concrete model (level 1)
      SubClassification.cs            -- Concrete model (level > 1)
      Hierarchy.cs                    -- Concrete model (tree structure)
      MigrationConfig.cs              -- Config POCO
    Services/
      CouchbaseConnectionManager.cs   -- Cluster/bucket connections
      GuidRemapper.cs                 -- Old->New GUID mapping
      DataFetcher.cs                  -- Fetches all 3 collections from source
      DocumentTransformer.cs          -- Applies new GUIDs, nulls arrays
      DocumentUpserter.cs             -- Batched upsert to target
      MigrationRunner.cs              -- Main orchestrator
```

---

## Document Structures (from samples)

### Classification (level 1)

`META().id` equals the `id` field inside the document.

| Field | Type | Notes |
| --- | --- | --- |
| id | string (GUID) | Document key. Gets NEW GUID on migration |
| companyId | int | Filter: 1 |
| classification | string | e.g. "Neem" |
| name | string | Display name |
| image | string | Image filename |
| nodelevel | int | Always 0 for classification |
| navigationRoute | string | e.g. "node/home" |
| microlearning | array | Set to `null` on migration |
| jobbing | array | Set to `null` on migration |
| tracebility | array | Set to `null` on migration |
| microLearningCount | int | Set to `0` on migration |
| jobbingCount | int | Set to `0` on migration |
| traceabilityCount | int | Set to `0` on migration |
| subCategoryCount | int | Preserved |
| isDeleted | bool | Preserved |
| isAnimalCategory | bool | Preserved |
| createdBy | string | Preserved |
| createdDate | datetime | Preserved |
| lastModifiedBy | string | Preserved |
| lastModifiedDate | datetime | Preserved |

### SubClassification (level > 1)

`META().id` equals the `id` field inside the document.

| Field | Type | Notes |
| --- | --- | --- |
| id | string (GUID) | Document key. Gets NEW GUID on migration |
| parentId | string (GUID) | Reference to parent. Remapped to NEW GUID |
| parent | string | Parent name (string, unchanged) |
| name | string | Display name |
| image | string | Image filename |
| icon | string | Icon filename |
| description | string | Text description |
| leaflevel | int | e.g. 2, 3, etc. |
| yield | double | Preserved |
| wet | double | Preserved |
| days | int | Preserved |
| duration | int | Preserved |
| microlearning | array | Set to `null` on migration |
| jobbing | array | Set to `null` on migration |
| tracebility | array | Set to `null` on migration |
| microLearningCount | int | Set to `0` on migration |
| jobbingCount | int | Set to `0` on migration |
| traceabilityCount | int | Set to `0` on migration |
| tableData | array | Preserved |
| isDeleted | bool | Preserved |
| createdBy | string | Preserved |
| createdDate | datetime | Preserved |
| lastModifiedBy | string | Preserved |
| lastModifiedDate | datetime | Preserved |

### Hierarchy

`META().id` equals the `id` field inside the document.

| Field | Type | Notes |
| --- | --- | --- |
| id | string (GUID) | Same GUID as corresponding Classification/SubClassification. Remapped |
| parentId | string (GUID) | Parent node GUID. Remapped (null for root) |
| name | string | Display name |
| parentname | string | Parent display name (unchanged) |
| level | int | 1 = Classification, > 1 = SubClassification |
| imageUrl | string | Image URL |
| companyId | int | Company filter |
| isDeleted | bool | Preserved |
| createdBy | string | Preserved |
| createdDate | datetime | Preserved |
| lastModifiedBy | string | Preserved |
| lastModifiedDate | datetime | Preserved |

---

## Relationships

```text
Classification (level 1)
    |
    |-- 1:N --> SubClassification (level 2+)
    |               |
    |               |-- parentId -> Classification.id  (or another SubClassification.id)
    |
    |-- Hierarchy (level 1, parentId = null)
            |
            |-- Hierarchy (level 2+, parentId = parent Hierarchy.id)
```

- Classification `id` = Hierarchy `id` (for level 1)
- SubClassification `id` = Hierarchy `id` (for level > 1)
- SubClassification `parentId` = parent Classification/SubClassification `id`
- Hierarchy `parentId` = parent Hierarchy `id` (which is also a Classification/SubClassification `id`)

---

## Migration Algorithm

```text
Step 1:  Connect to source + target clusters
Step 2:  Ensure UCG scope + 3 collections exist on target
Step 3:  Fetch all Hierarchy from source WHERE companyId = 1
Step 4:  Separate hierarchy by level:
           level = 1  --> classification IDs
           level > 1  --> subclassification IDs
Step 5:  Fetch Classification docs by those IDs from source
Step 6:  Fetch SubClassification docs by those IDs from source
Step 7:  Register ALL IDs in GuidRemapper (BEFORE any transform)
           ** Critical: must happen before Steps 8-10 **
Step 8:  Transform Classification:
           id = newGUID
           microlearning / jobbing / tracebility = null
           counts = 0
Step 9:  Transform SubClassification:
           id = newGUID
           parentId = remapped newGUID
           microlearning / jobbing / tracebility = null
           counts = 0
Step 10: Transform Hierarchy:
           id = remapped newGUID (same as Classification/SubClassification)
           parentId = remapped newGUID (null stays null)
Step 11: Upsert all to target (batched, 50 parallel)
Step 12: Verify counts + log GUID mapping
```

---

## N1QL Queries

### Fetch Hierarchy (filtered by companyId)

```sql
SELECT META(h).id AS docId, h.*
FROM `PRODDATA`.`UCG`.`Hierarchy` h
WHERE h.companyId = 1
```

### Fetch Classification by IDs (batched, 100 IDs per query)

```sql
SELECT META(c).id AS docId, c.*
FROM `PRODDATA`.`UCG`.`Classification` c
WHERE META(c).id IN $ids
```

### Fetch SubClassification by IDs (batched, 100 IDs per query)

```sql
SELECT META(s).id AS docId, s.*
FROM `PRODDATA`.`UCG`.`SubClassification` s
WHERE META(s).id IN $ids
```

---

## Transformation Summary

| Field | Classification | SubClassification | Hierarchy |
| --- | --- | --- | --- |
| Document key | New GUID | New GUID | New GUID |
| `id` | New GUID | New GUID | Remapped GUID |
| `parentId` | N/A | Remapped GUID | Remapped GUID (null stays null) |
| `microlearning` | `null` | `null` | N/A |
| `jobbing` | `null` | `null` | N/A |
| `tracebility` | `null` | `null` | N/A |
| Counts | `0` | `0` | N/A |
| Other fields | Preserved | Preserved | Preserved |

---

## Milestones

### Milestone 1: Project Setup + Models

- Create new .NET 8.0 console project with `dotnet new console`
- Add NuGet packages:
  - `CouchbaseNetClient`
  - `Microsoft.Extensions.Configuration.Json`
  - `Microsoft.Extensions.Configuration.Binder`
- Create `appsettings.json` with source/target connection config
- Create concrete model classes:
  - `Models/Classification.cs`
  - `Models/SubClassification.cs`
  - `Models/Hierarchy.cs`
  - `Models/MigrationConfig.cs`

### Milestone 2: Core Services

- `Services/CouchbaseConnectionManager.cs` -- connect source + target clusters, dispose
- `Services/GuidRemapper.cs` -- dictionary-based old-to-new GUID mapping
- `Services/DataFetcher.cs` -- N1QL queries to fetch Hierarchy (filtered), Classification (by IDs), SubClassification (by IDs)

### Milestone 3: Transformation + Upsert

- `Services/DocumentTransformer.cs` -- apply new GUIDs, null arrays, remap parentIds
- `Services/DocumentUpserter.cs` -- batched parallel upsert to target

### Milestone 4: Orchestration + Verification

- `Services/MigrationRunner.cs` -- ties all steps together in correct order
- `Program.cs` -- entry point, config loading, run migration
- Post-migration verification:
  - Count queries on target collections
  - Referential integrity check (SubClassification parentId exists in target)
  - GUID mapping log for audit trail

---

## Verification Checklist

1. `dotnet build` -- zero errors
2. Run migration -- logs show fetch counts for all 3 collections
3. Query target: document counts match expected
4. All target document keys are new GUIDs (none match source)
5. Every SubClassification `parentId` exists as a key in target Classification or SubClassification
6. Every Hierarchy `id` matches a target Classification/SubClassification key
7. Sample docs have `microlearning: null`, `jobbing: null`, `tracebility: null`
