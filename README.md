# Hoarder's Forge: Recycle and Reclaim

A *Vintage Story* utility mod that lets you recycle your old, worn-out tools, broken toolheads, arrowheads, metal chutes, and leftover metal scraps by melting them back in the crucible to reclaim their metal value.

**Mod ID:** `hoardersforge`

---

## 🌟 Features

- **Reclaim Metal Scraps:** Throw your old tools, damaged toolheads, arrowheads, metal brackets, padlock items, and metal chutes directly into the crucible to melt them down.
- **Dynamic Voxel-Based Yields:** Integrates with the **SmithingPlus** mod to dynamically calculate the metal yield based on the exact voxel count of the item's recipe (proportional to a standard 100-unit ingot). 
- **Durability Decay:** Reclaimed metal scales proportionally to the remaining durability of the tool, ensuring worn tools return less metal than pristine ones.
- **Safe Anti-Exploit Math:** Yield calculations round down to the nearest multiple of 5 units of liquid metal, preventing duplication exploits and keeping crucible volumes clean of awkward fractional remnants.
- **Vanilla Compatibility:** Seamlessly falls back to a 100u / 200u standard yield system when running in vanilla (without SmithingPlus).

---

## ⚖️ Melting Calculations

The amount of liquid metal reclaimed from an item is calculated as follows:

### With SmithingPlus Enabled (Proportional & Rounded)
For finished items:
$$\text{Reclaimed Units} = \lfloor \frac{\text{Recipe Voxel Count} \times \frac{100.0}{42.0} \times \text{Durability Ratio}}{5.0} \rfloor \times 5.0$$
*Note: A minimum yield of 5 units is guaranteed for any valid smithed item to prevent total metal loss.*

For unfinished workitems:
$$\text{Reclaimed Units} = \lfloor \frac{\text{Current Voxel Count} \times \frac{100.0}{42.0}}{5.0} \rfloor \times 5.0$$

### Without SmithingPlus (Vanilla Fallback)
* Tools and standard tool heads yield **100 units** of liquid metal (or less based on durability).
* Large parts (plates, long blades, sword blades) yield **200 units** of liquid metal.
* Arrowheads yield **10 units** of liquid metal.

---

## 💾 Installation

1. Place the mod `.zip` archive into your `%APPDATA%/VintagestoryData/Mods/` directory.
2. Start the game.

## 🛠️ Development & Packaging

To compile the C# assembly and package the mod files into a deployable zip, run the PowerShell build script in the root directory:
```powershell
.\build.ps1
```

## 📝 License & Credits
* **Authors:** JimmyJTC & Antigravity
* **License:** This project is licensed under the **GNU General Public License v3.0 (GPLv3)**. See the [LICENSE](LICENSE) file for details.
