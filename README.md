# Solar Expanse Storage Limits

A BepInEx/Harmony mod for Solar Expanse that gives resources real local storage
capacity. Storage depots matter, full stockpiles slow down production, and the
resource UI shows how close each managed resource is to its cap.

## Features

- Adds per-location storage caps for configured resources.
- Makes storage facilities increase capacity for specific resources.
- Supports different limits for surface and orbital storage.
- Counts docked spacecraft fuel tanks as storage for their matching propellant.
- Slows mining, refining, and byproduct production as output storage fills up.
- Stops production for capped output resources when storage is full.
- Keeps cargo deliveries accepted even when they push stock above the cap.
- Gradually removes configured over-cap stock through spoilage.
- Returns spoiled natural resources to local deposits when the game supports it.
- Adds storage fill bars to resource icons in the object information UI.
- Adds storage totals, fill percent, and remaining capacity to resource tooltips.
- Adds storage capacity entries to facility stat panels.

## How It Works

Every managed resource gets a capacity at each player-controlled location:

```text
base capacity + storage granted by completed facilities + applicable docked fuel tanks
```

The configuration can define small early storage, larger bulk/general storage,
and cryogenic storage for gases and propellants. Resources can be limited on the
surface, in orbit, or both.

Production is gently throttled during the last 5% of available storage. Once a
resource is at or above its cap, facilities that output that resource stop
producing it until space is available again.

If a location has more than its cap, configured resources slowly decay at their
per-resource spoilage rate. Natural resources that can exist as deposits are
returned to the local deposit pool; manufactured resources are simply lost.

## Installation

1. Install BepInEx for Solar Expanse.
2. Download the latest release.
3. Copy the included `StorageLimits` folder into:

   ```text
   Solar Expanse/BepInEx/plugins/
   ```

4. Make sure these files are present together:

   ```text
   BepInEx/plugins/StorageLimits/StorageLimits.dll
   BepInEx/plugins/StorageLimits/YamlDotNet.dll
   BepInEx/plugins/StorageLimits/storage_limits.yaml
   ```

5. Launch the game.

The mod loads once a solar system is loaded. If something goes wrong, check the
BepInEx log for entries beginning with `[StorageLimits]`.

## Configuration

Storage behavior is controlled by:

```text
BepInEx/plugins/StorageLimits/storage_limits.yaml
```

Important settings:

- `baseCapacity`: default surface storage for each managed resource.
- `baseCapacityOrbit`: default orbital storage for each managed resource.
- `facilities`: facility IDs mapped to the resources and capacity they add.
- `placement`: optional per-resource placement, using `surface`, `orbit`, or
  `both`.
- `spoilage.rates`: per-resource percent of excess stock removed per game day.

Example:

```yaml
baseCapacity: 3000
baseCapacityOrbit: 0

facilities:
  build_storage_bulk:
    id_resource_steel: 10000
    id_resource_metal: 10000

placement:
  id_resource_steel: surface
  id_resource_metal: surface

spoilage:
  rates:
    id_resource_steel: 0.001
    id_resource_metal: 0.001
```

Only resources that appear in a facility grant are managed by the mod. Resources
not listed there keep the game's normal unlimited behavior.

## Compatibility Notes

- Requires BepInEx and Harmony.
- The plugin has a soft dependency on `com.teddit.teddit`, but it can load
  without it.
- This mod changes production efficiency calculations for managed resources, so
  other mods that patch facility production may need testing alongside it.
