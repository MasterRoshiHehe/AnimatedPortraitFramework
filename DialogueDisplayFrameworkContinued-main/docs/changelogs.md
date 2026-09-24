# Changelogs

## 0.7.6
 * Fixed dialogue box size and offset not being applied when using some dialogue-altering mods or when repeating the debug command.
 * Fixed incompatible display with Platonic Relationships mod.

## 0.7.5
 * Fixed incorrect placement after dialogue transition on Android.

## 0.7.4
 * Fixed dialogue text not adjusted to config.
 * Fixed box size re-applied every tick.
 * Fixed box config not applied on window resize.
 * Potentially fixed position reset between dialogues.

## 0.7.3
 * Added config settings to manually adjust the box's size and position.
 * Fixed changes not updated after patch reload.
 * Fixed box size and position not updated after patch reload.

## 0.7.2
 * Fixed incompatibility with MEEP farm animals.
 * Fixed dialogue crash when other mods dispose of portrait textures.

## 0.7.1
 * Added an option to scale the gift icon independently of the checkmarks.
 * Fixed scale not affecting gift components.
 * Fixed misaligned friendship jewel.
 * Fixed sized boxes also applying to question boxes.
 * Fixed misplaced friendship jewel tooltip.
 * Fixed an incompatible method missing from SMAPI for Android. Please report further issues.

## 0.7.0
* Introduced an API that allows mods to access the framework's data and render methods.
* Fixed a crash when the `dialogue` field was set to null, again.
* Fixed potential issues relating to Harmony on other OS.
* Fixed a typo in a warning message.

## 0.6.0
* The `default` entry now starts with preset values instead of being empty. This allows mods to make single-value changes instead of requiring the entire dialogue.
* Added a `copyFrom` field which fills null fields and merges lists with data from another entry.
* Added support for patches needing to be applied only with specific appearances/locations.
* Added `ID` fields to image, text and divider objects for inter-mod support.
* Added support for the `disabled` fields on image, text and divider objects.
* Hearts changes:
    * Added `showPartialHearts` field to allow mods to only show full heart images.
    * Added support for `layerDepth` and `scale` fields.
    * Fixed remaider hearts on the final row not being centered.
    * Fixed hearts showing slightly off-centered.
* Divider changes:
	* Default height now sets to the dialogue box's height.
    * Added a `connectors` field to include the connection image at the ends of dividers.
    * Added support for `right` and `bottom` fields.
    * Replaced the `red`, `green` and `blue` fields with a `color` field which supports more color formats.
* Removed `tilesheet` field from portrait data. Instead, directly assign a value to `x` and `y`.
* Optimized asset caching to improved performance while the dialogue box is open.
* Fixed an issue with array objects not being edited properly by Content Patcher.
* Fixed crashes when the `dialogue` field is missing or removed.
* Fixed potential crash during dialogues with NPCs not yet spawned.
* Removed support for comma-separated lists. Use `copyFrom` instead.
* Removed api docs from release files. Mod authors can always read them online.

## 0.5.2
* Patch reloading no longer requires re-opening the current dialogue to see changes.

## 0.5.1
* Fixed dialogue crashing on custom NPCs.

## 0.5.0
* Initial release based on [Dialogue Display Framework](https://github.com/aedenthorn/StardewValleyMods/tree/master/DialogueDisplayFramework).
* Added support for listed key.
* Updated support for colors to use HEX, RGB and named colors.
* Fixed incompatibility with the 1.6 SDV update.
* Disabled support for sprite data.
* Disabled support for Mod Updater.