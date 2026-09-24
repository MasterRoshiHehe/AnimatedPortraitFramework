This document is intended to help mod authors create content packs using Dialogue Display Framework Continued.

## Contents
- [Intro](#intro)
  - [What is DDFC?](#what-is-ddfc)
  - [Why use DDFC?](#why-use-ddfc)
- [Getting started](#getting-started)
  - [Creating a content pack](#creating-a-content-pack)
  - [Adding changes](#adding-changes)
  - [Testing changes](#testing-changes)
- [Compatibility support](#compatibility-support)
  - [Fields vs Entries](#fields-vs-entries)
  - [Target field](#target-field)
  - [Load order](#load-order)
- [Entry keys](#entry-keys)
- [Entry fields](#entry-fields)
  - [Base fields](#base-fields)
  - [Common data fields](#common-data-fields)
  - [Dialogue fields](#dialogue-fields)
  - [Portrait fields](#portrait-fields)
  - [Hearts fields](#hearts-fields)
  - [Gift fields](#gift-fields)
  - [Image fields](#image-fields)
  - [Text fields](#text-fields)
  - [Divider fields](#divider-fields)
  - [Connectors fields](#connectors-fields)
- [Game state queries](#game-state-queries)
- [Examples](#examples)
- [Advanced examples](#advanced-examples)

## Intro

### What is DDFC?

Dialogue Display Framework Continued (DDFC) is the continuation of aedenthorn's [Dialogue Display Framework](https://www.nexusmods.com/stardewvalley/mods/11661) (DDF) to support the 1.6 updates, but also to improve its features and implementation. The framework is a valuable tool for any mod authors looking to edit the dialogue box's user interface.

### Why use DDFC?

Dialogue boxes are hard-coded into the game, making editing parts of the interface, while keeping compatibility between mods, not easily feasible without a common framework. Thankfully, not only does DDFC offer such common grounds for compatibility, but it also does so without needing any knowledge of game code. With a few content patches made with JSON, mod authors can change the dialogue box's UI in creative ways while keeping compatibility with other mods doing the same.

## Getting started

### Creating a content pack

If you've never created a Content Patcher pack before, I strongly advise reading [this guide](https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide.md#create-the-content-pack) first.

Adding the framework as a dependency is not a requirement, but it'll make it more obvious when users forget to install it along your mod. Creating the dependency is as simple as adding the following lines to your `manifest.json`:
```json
"Dependencies": [
    {
        "UniqueID": "Mangupix.DialogueDisplayFrameworkContinued",
        "IsRequired": true
    },
]   
```

### Adding changes

All changes to the interface are made by targeting the same dictionary object and usually have the following structure:

```json
{
    "Format": "2.0.0",
    "Changes": [
        {
            "Action": "EditData",
            "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
            "Fields": {
                "default": {
                    (your changes go here)
                }
            }
        }
    ]
}
```

In most cases, the only targetted entry will be `default`, which means the changes apply to all game characters (with a portrait frame). Other supported keys include character names, for when changes only apply to some characters. For more details, see [entry keys](#entry-keys).

Each entry uses the same [data model](#data-fields), described in later sections, and can be modified using Content Patcher's `EditData` action. You can apply multiple patches to the same entries, and it's sometimes even encouraged (e.g. when using `TargetField` or `HasMod`). See Content Patcher's [`Action: EditData` documentation](https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-editdata.md) for more info. 

When unmodified, the `default` entry contains values matching the normal appearance of dialogues without the framework (see [default data](/docs/defaults.json)). This lets you do single-value changes while leaving the rest of the dialogue box unchanged. This is not the case for other entry keys, which should use the `copyFrom` field for the same effect.

For example, the following entry changes the jewel's position when talking to Emily while keeping the rest of the UI unchanged:

```json
"Emily": {
    "copyFrom": "default",
    "jewel": {
        "xOffset": -60,
        "yOffset": 60,
        "right": true
    }
}
```

### Testing changes

When testing for changes in-game, you can use the following command to open unlimited dialogues anywhere: 

```
debug dialogue <npcName> <dialogueString>
```

See [debug commands](https://stardewvalleywiki.com/Modding:Console_commands#Dialogue) for more info.

To refresh any changes without re-opening the game (or dialogue), you can use the following command:

```
patch reload <yourModID>
```

## Compatibility support

While true that mods can increase compatibility with patches using `HasMod` conditions, this isn't always necessary. In fact, patches with high compatibility will change as few values as possible, reducing the chance of conflicting changes with other mods. Thankfully, several options are available.

### Fields vs Entries

When creating a patch, at least one of the following fields must be used:

<table>
<tr>
<th>Field</th>
<th>Purpose</th>
</tr>
<td>

`Fields`

</td>
<td>

Assigns some fields while leaving the rest unchanged. Recommended if your mod needs full control over specific elements.

**Caveat:** Supplied objects and lists replace the entire field values. It does *not* just change the given fields in those objects!

For example, despite the name element being previously positioned, the following patch replaces *all* data, repositioning it to a default `(0,0)` offset.

```json
"Fields": {
    "default": {
        "name": { "color": "black" }
    }
}
```

Similarly with lists, assigning a new entry will erase all other list entries.

For this reason, you should use [`TargetField`](#target-field) when adding or editing list entries and when you only modify a few fields. Avoid copying default values, as this reduces compatibility.

</td>
</tr>
<tr><td>

`Entries`

</td><td>

Replaces entire entries to be the supplied object, potentially erasing changes made by other mods. Not recommended to use on the `default` entry.

For example, the following patch creates a new entry for Emily based on the data from `default`. However, this will overwrite any previous changes that happened earlier in the [load order](#load-order).
```json
"Entries": {
    "Emily": {
        "copyFrom": "default",
        "portrait": { ... }
    }
}
```

Must be used if a `TargetField` is specified.

</td></tr>
<tr>
<td>

`MoveEntries`

</td>
<td>

Moves image, text and divider entries to be drawn before or after other mod's assets.

Objects lower in the list will be drawn last.

See also the `layerDepth` field for a similar effect.

</td></tr></table>

### Target field

Using `TargetField` is ideal when changing a few fields or editing entries in lists (images, texts, dividers). See [target field documentation](https://github.com/Pathoschild/StardewMods/blob/stable/ContentPatcher/docs/author-guide/action-editdata.md#target-field) for more info.

For example, the following patch re-aligns the name text vertically while leaving the horizontal position unchanged:
```json
"TargetField": [ "default", "name" ],
"Entries": {
    "yOffset": -100,
    "bottom": true
}
```

When adding or editing images, texts or dividers, you **must** use `TargetField` to maintain the integrity of those lists. For example, this is how to add a new image entry:

```json
"TargetField": [ "default", "images" ],
"Entries": {
    "YourModID.NewImage": {
        "ID": "YourModID.NewImage",
        (your data here)
    }
}
```

### Load order

Changing the load order is another good way to support compatibility. If you're aware of changes another mod does, you can tweak the appearance to better match your mod by using an [optional dependency](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest#Dependencies) to make sure their changes are already applied when your patches are loaded.

Content Patcher also has a `Priority` field that allows you to change when your mod should be applied, but this can be unreliable and cause issues. As a rule of thumb, only set a `Priority: Low` to initialize empty entries (e.g. for character keys) then later edit them as normal. This way, no data is lost.

## Entry keys

Entry keys must be one of the following:

| Key                                  | Description |
| ------------------------------------ | ----------- |
| `<CharacterNameID>_<LocationNameID>` | ***Legacy support:** Edit NPC appearance data instead.*<br>Apply changes to a character listed in a location's `UniquePortrait` property
| `<CharacterNameID>_<AppearanceID>`   | Apply changes to a character using a specified appearance ID. Might not match their current texture if they were manually overridden elsewhere
| `<CharacterNameID>_Beach`            | Apply changes to a character in beach attire
| `<CharacterNameID>`                  | Apply changes to a specific character
| `default`                            | Fallback option if no other data is specified or for a global dialogue setup

**Note:** Patches will be overridden in the order given above, top being checked first and bottom being checked last. For example, beach keys will take precedence over those with standard keys but will be replaced by appearance keys and location keys.

## Entry fields

### Base fields

Dictionary entries have the following optional fields:

| Key        | Type    | Description |
| ---------- | ------- | ----------- |
| `packName` | string  | (Optional) Manifest ID of the content pack containing this entry.
| `copyFrom` | string  | Key to a data entry to copy data from. Any following data will override the copied data.
| `xOffset`  | integer | X offset of the dialogue box relative to its normal position on the screen.
| `yOffset`  | integer | Y offset of the dialogue box relative to its normal position on the screen.
| `width`    | integer | Width of the dialogue box, default 1200.
| `height`   | integer | Height of the dialogue box, default 384.
| `dialogue` | [Dialogue](#dialogue-fields)        | Customizes the dialogue string display.
| `portrait` | [Portrait](#portrait-fields)        | Customizes the character's portrait image display.<br>Doesn't include the portrait frame background.
| `name`     | [Text](#text-fields)                | Customizes the name display which normally appears under the portrait frame.
| `jewel`    | [Common data](#common-data-fields)  | Customizes the friendship jewel display.
| `button`   | [Common data](#common-data-fields)  | Customizes the action button display.
| `gifts`    | [Gifts](#gifts-fields)              | Customizes a custom gift display.
| `hearts`   | [Hearts](#hearts-fields)            | Customizes a friendship hearts display.
| `images`   | List of [Images](#image-fields)     | Custom images to draw. Assigning a value of `null` will erase pre-existing entries, otherwise it will merge the lists.
| `texts`    | List of [Texts](#text-fields)       | Custom texts to draw. Assigning a value of `null` will erase pre-existing entries, otherwise it will merge the lists.
| `dividers` | List of [Dividers](#divider-fields) | Custom dividers to draw. Assigning a value of `null` will erase pre-existing entries, otherwise it will merge the lists.
| `disabled` | boolean | Whether to disable the entry entirely, default false.<br>Similar to setting the entry to `null` but with the option of enabling it in later patches.
| `condition`| string  | Check for [Game State Query](https://wiki.stardewvalley.net/Modding:Game_state_queries).

If any of the above fields are missing, the value will be taken from the [defaults](/docs/defaults.json) preset matching the unmodded game's dialogue.

### Common data fields

All root entry objects also have the following common fields available (although, some objects might not use them all):

| Key          | Type    | Description |
| ------------ | ------- | ----------- |
| `xOffset`    | integer | X offset relative to the box, default 0.
| `yOffset`    | integer | Y offset relative to the box, default 0.
| `right`      | boolean | Whether the x offset should be calculated from the right side of the box, default false.
| `bottom`     | boolean | Whether the y offset should be calculated from the bottom of the box, default false.
| `width`      | integer | Width of elements that need it.
| `height`     | integer | Height of elements that need it.
| `alpha`      | decimal | Opacity, default 1 (full opacity).
| `scale`      | decimal | Size scale, default 4 (most things in the game are displayed at 4x).
| `layerDepth` | decimal | A higher value will be drawn "on top" of elements with lower values, similar to the `z-index` property of CSS, default 0.88.
| `disabled`   | boolean | Whether to disable the element entirely and use any values from `copyFrom`, default false.<br>Similar to setting the field to `null` but with the option of enabling it in later patches.

## `Dialogue` fields

Along the [common data fields](#common-data-fields), dialogue data includes the following fields:

| Key         | Type   | Description |
| ----------- | ------ | ----------- |
| `color`     | string | Supports color name, hex and RGB formats.<br>See [color formats](https://stardewvalleywiki.com/Modding:Common_data_field_types#Color) for more info.
| `alignment` | enum   | Text alignment: 0 = left, 1 = center, 2 = right.


## `Portrait` fields

Along the [common data fields](#common-data-fields), portrait data includes the following fields:

| Key           | Type    | Description |
| ------------- | ------- | ----------- |
| `texturePath` | string  | The asset name for the portrait sheet texture to display, using the character's default portrait sheet by default.<br><br>**Note:** Make sure to load any custom textures first!<br><br>**Warning:** This may overwrite any portrait sheet change, both through normal interaction (e.g. beach attire, appearance change, etc.) and during events.
| `x`           | integer | X position in the source texture file, default -1 (disabled).
| `y`           | integer | Y position in the source texture file, default -1 (disabled).
| `w`           | integer | Width in the source texture file, default 64.
| `h`           | integer | Height in the source texture file, default 64.

## `Hearts` fields

Along the [common data fields](#common-data-fields), hearts data includes the following fields:

| Key                 | Type    | Description |
| ------------------- | ------- | ----------- |
| `heartsPerRow`      | integer | Number of hearts per row, default 14.
| `showEmptyHearts`   | boolean | Display empty hearts, default true.
| `showPartialHearts` | boolean | Display partial hearts, default true.
| `centered`          | boolean | If true, `xOffset` will point to the center of the row of hearts.


## `Gift` fields

Along the [common data fields](#common-data-fields), gift data includes the following fields:

| Key            | Type    | Description |
| -------------- | ------- | ----------- |
| `iconScale`    | decimal | Scale specific to the gift icon, default 1. Set to 0 to hide. Multiplied by `scale`.
| `inline`       | boolean | If true, the check boxes are placed to the right of the gift icon, otherwise it's placed underneath, default false.


## `Image` fields

The `images` field is a list of objects with [common data fields](#common-data-fields) and the following fields:

| Key           | Type    | Description |
| ------------- | ------- | ----------- |
| `id`          | string  | (Required) A [unique string ID](https://stardewvalleywiki.com/Modding:Common_data_field_types#Unique_string_ID) for inter-mod support, `MISSING_ID` by default.
| `texturePath` | string  | (Required) The asset name of the texture to display.<br><br>**Note:** Make sure to load any custom textures first!
| `x`           | integer | X position in the source texture file.
| `y`           | integer | Y position in the source texture file.
| `w`           | integer | Width in the source texture file.
| `h`           | integer | Height in the source texture file.


## `Text` fields

The `texts` field is a list of objects with [common data fields](#common-data-fields) and the following fields:

| Key               | Type    | Description |
| ----------------- | ------- | ----------- |
| `id`              | string  | (Required) A [unique string ID](https://stardewvalleywiki.com/Modding:Common_data_field_types#Unique_string_ID) for inter-mod support, `MISSING_ID` by default<br>Unused with the `name` field.
| `text`            | string  | The text to display. Unused with the `name` field.
| `placeholderText` | string  | If using scroll background, this affects the size of the scroll while keeping it centered.
| `color`           | string  | Supports color name, hex and RGB formats.<br>See [color formats](https://stardewvalleywiki.com/Modding:Common_data_field_types#Color) for more info.
| `scrollType`      | integer | Possible values:<br>`-1` = No scroll (default),<br>`0` = Sizeable scroll,<br>`1` = Speech bubble,<br>`2` = Cave depth plate,<br>`3` = Mastery text plate
| `junimo`          | boolean | Whether the name should be displayed in Junimo characters, because why not.
| `alignment`       | enum    | Possible values:<br>`0` = Left,<br>`1` = Center (default),<br>`2` = Right
| `centered`        | boolean | **Legacy support:** Use `alignment: 1`.<br>Whether to center the text at the given position.


## `Divider` fields

The `dividers` field is a list of objects with [common data fields](#common-data-fields) and the following fields:

| Key          | Type    | Description |
| ------------ | ------- | ----------- |
| `id`         | string  | (Required) A [unique string ID](https://stardewvalleywiki.com/Modding:Common_data_field_types#Unique_string_ID) for inter-mod support, `MISSING_ID` by default.
| `horizontal` | boolean | Horizontal, default false (i.e. vertical).
| `connectors` | [Connectors](#connector-fields) | Customize the connector image at the ends of the divider. When `horizontal` is false, both connectors are enabled by default.
| `color`      | string  | Supports color name, hex and RGB formats<br>See [color formats](https://stardewvalleywiki.com/Modding:Common_data_field_types#Color) for more info.<br><br>**Note:** When specified, `Maps\MenuTilesUncolored` will be used instead of `Maps\MenuTiles`.

## `Connectors` fields

The `connectors` field refers to an object with the following fields:

| Key      | Type    | Description |
| ---------| ------- | ----------- |
| `top`    | boolean | Whether or not to display the top connector, default true.
| `bottom` | boolean | Whether or not to display the bottom connector, default true.

## Game state queries

The following game state queries (GSQ) are provided for use in the `condition` field of entries. They won't have any effect elsewhere, including in Content Patcher's query conditions.

| Condition                                              | Effect                     |
| ------------------------------------------------------ | ---------------------------|
| `Mangupix.DDFC_SPEAKER_NAME <name>`                    | Whether the supplied argument matches the speaker's name. Case insensitive.
| `Mangupix.DDFC_SPEAKER_GENDER <gender>`                | Whether the supplied gender identity matches the speaker's. Options are `female`, `male` or `undefined`. Case insensitive.
| `Mangupix.DDFC_SPEAKER_APPEARANCE_ID <id>`             | Whether the supplied appearance ID matches the speaker's current appearance. Case insensitive.
| `Mangupix.DDFC_SPEAKER_FRIENDSHIP_POINTS <min> <max>`  | Whether the speaker's friendship points is between `<min>` and `<max>`, inclusively.
| `Mangupix.DDFC_SPEAKER_CAN_RECEIVE_GIFTS`              | Whether the speaker can be given gifts.
| `Mangupix.DDFC_SPEAKER_CAN_SOCIALIZE`                  | Whether the speaker can be befriended.
| `Mangupix.DDFC_SPEAKER_CAN_BE_ROMANCED `               | Whether the speaker can be romanced.

## Examples

### HD portraits

Increases the portrait's resolution by 8x. Note that the default scale is 4, so making it 8 times smaller would use a scale of 0.5.
```json
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "TargetField": [ "default", "portrait" ],
    "Entries": { "h": 512, "w": 512, "scale": 0.5 }
}
```

### Floating portrait

Moves the name and portrait above the dialogue box

```json
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "Entries": {
        "default": {
            "dialogue": { "width": 1000 },
            "name": {
                "xOffset": 20,
                "yOffset": -100,
                "alignment": 0
            },
            "portrait": {
                "xOffset": 60,
                "yOffset": -360
            },
            "jewel": {
                "xOffset": -64,
                "yOffset": -52,
                "right": true,
                "bottom": true
            },
            "button": {
                "xOffset": -40,
                "yOffset": -40,
                "right": true,
                "bottom": true
            }
        }
    }
},
```

### Speech bubble

Adds a speech bubble above Abigail's portrait.

```json
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "Entries": {
        "Abigail": { "CopyFrom": "default" }
    },
    "Priority": "Early"
},
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "TargetField": [ "Abigail", "texts" ],
    "Entries": {
        "ExampleMod.AbigailSpeech": {
            "ID": "ExampleMod.AbigailSpeech",
            "text": "Yummy amethysts...",
            "xOffset": -280, "yOffset": -24, "right": true,
            "scrollType": 1,
        }
    }
}
```

## Advanced examples

### Multi-Character patching

Gives a pink name color to all bachelors and bachelorettes.

```json
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "Entries": {
        "Sebastian": { "CopyFrom": "default" },
        "Abigail": { "CopyFrom": "Sebastian" },
        "Harvey": { "CopyFrom": "Sebastian" },
        "Eliott": { "CopyFrom": "Sebastian" },
        "Shane": { "CopyFrom": "Sebastian" },
        "Emily": { "CopyFrom": "Sebastian" },
        "Haley": { "CopyFrom": "Sebastian" },
        "Penny": { "CopyFrom": "Sebastian" },
        "Alex": { "CopyFrom": "Sebastian" },
        "Leah": { "CopyFrom": "Sebastian" },
        "Maru": { "CopyFrom": "Sebastian" },
        "Sam": { "CopyFrom": "Sebastian" }
    },
    "Priority": "Early"
},
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "TargetField": [ "Sebastian", "name" ],
    "Entries": {
        "color": "pink"
    }
}
```

### Swap portrait and name

```json
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "Fields": {
        "default": {
            "name": {
                "xOffset": -222,
                "yOffset": 20,
                "right": true
            },
            "portrait": {
                "xOffset": -352,
                "yOffset": 104,
                "right": true
            },
            "jewel": {
                "xOffset": -64,
                "yOffset": -52,
                "right": true,
                "bottom": true
            }
        }
    },
},
{
    "Action": "EditData",
    "Target": "aedenthorn.DialogueDisplayFramework/dictionary",
    "TargetField": [ "default", "images" ],
    "Entries": {
        "ExampleMod.UpperPortrait": {
            "ID": "ExampleMod.UpperPortrait",
            "texturePath": "LooseSprites/Cursors",
            "xOffset": -428,
            "yOffset": 12,
            "right": true,
            "x": 589, "y": 489,
            "w": 102, "h": 18
        },
        "ExampleMod.LowerPortrait": {
            "ID": "ExampleMod.LowerPortrait",
            "texturePath": "LooseSprites/Cursors",
            "xOffset": -380,
            "yOffset": 84,
            "right": true,
            "x": 601, "y": 414,
            "w": 76, "h": 74
        }
    }
}
```
