# Solar Expanse Storage Limits

A BepInEx/[Teddit](https://github.com/ted505/solar-expanse-teddit) mod for Solar Expanse that gives resources real local storage
capacity per locations. Cryogenic storage needs dedicated tanks, while other resources will need warehouses -- no longer can you plop down a single mining module, wait 50 years, and come back to a 50kT of rare metals.

<img width="343" height="158" alt="image" src="https://github.com/user-attachments/assets/d8c9ec4f-dca3-47d4-9238-3b522d422967" />


## Features

- Adds per-location storage caps for configured resources.
- Two major resource storage types by default: cryogenic and general. Cryogenic storage takes special (power-hungry) tanks to use. I recommend using Orbital Overhaul with this mod for orbital energy production!
- 3 tiers of storage for each kind of resource: field, general, and bulk.
- Makes storage facilities increase capacity for specific resources.
- Counts docked spacecraft fuel tanks as storage for their matching propellant.
- Keeps cargo deliveries accepted even when they push stock above the cap.
- Gradually removes configured over-cap stock through spoilage.
- Returns spoiled natural resources to local deposits when the game supports it.
- Adds storage fill bars to resource icons in the object information UI.

## How It Works

Every managed resource gets a capacity at each player-controlled location:

```text
base capacity + storage granted by completed facilities + applicable docked fuel tanks
```

Production is gently throttled during the last 5% of available storage. Once a
resource is at or above its cap, facilities that output that resource stop
producing it until space is available again.

If a location has more than its cap,  resources slowly decay at their
per-resource spoilage rate. Hydrogen decays in months, while metal takes decades to be lost to the elements. Natural resources that can exist as deposits are
returned to the local deposit pool; manufactured resources are simply lost.

## Installation

1. Install BepInEx for Solar Expanse.
2. Download the latest release.
3. Copy the included `StorageLimits` folder into: ```Solar Expanse/BepInEx/plugins/```
4. Drag and drop the "Teddit" folder into ```Solar Expanse/BepInEx/plugins/```
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

- Requires BepInEx and Teddit.
