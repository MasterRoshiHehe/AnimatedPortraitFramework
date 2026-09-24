using System.Linq;

namespace DialogueDisplayFramework.Data
{
    public class DataHelpers
    {
        public static readonly string MISSING_ID_STR = "MISSING_ID";

        public static DialogueDisplayData DefaultValues => new()
        {
            Width = 1200,
            Height = 384,
            Dialogue = new()
            {
                XOffset = 8,
                YOffset = 8,
                Width = 716
            },
            Portrait = new()
            {
                XOffset = -352,
                YOffset = 32,
                Right = true
            },
            Name = new()
            {
                XOffset = -222,
                YOffset = 320,
                Right = true
            },
            Jewel = new()
            {
                XOffset = -64,
                YOffset = 256,
                Right = true,
            },
            Button = new()
            {
                XOffset = -532,
                YOffset = -44,
                Right = true,
                Bottom = true
            },
            Images = new()
            {
                new()
                {
                    ID = "DialogueDisplayFramework.Images.PortraitBackground",
                    TexturePath = "LooseSprites/Cursors",
                    XOffset = -452,
                    Right = true,
                    X = 583,
                    Y = 411,
                    W = 115,
                    H = 97
                }
            },
            Dividers = new()
            {
                new() {
                    ID = "DialogueDisplayFramework.Dividers.PortraitDivider",
                    XOffset = -484,
                    Right = true
                }
            }
        };

        public static DialogueDisplayData MergeEntries(DialogueDisplayData entry, DialogueDisplayData filler)
        {
            return new DialogueDisplayData()
            {
                XOffset = entry.XOffset ?? filler.XOffset,
                YOffset = entry.YOffset ?? filler.YOffset,
                Width = entry.Width ?? filler.Width,
                Height = entry.Height ?? filler.Height,
                Dialogue = entry.Dialogue ?? filler.Dialogue,
                Portrait = entry.Portrait ?? filler.Portrait,
                Name = entry.Name ?? filler.Name,
                Jewel = entry.Jewel ?? filler.Jewel,
                Button = entry.Button ?? filler.Button,
                Gifts = entry.Gifts ?? filler.Gifts,
                Hearts = entry.Hearts ?? filler.Hearts,
                Disabled = entry.Disabled,
                Condition = entry.Condition,
                Images = (entry.Images is null || filler.Images is null) ? entry.Images ?? filler.Images : filler.Images.Concat(entry.Images).ToList(),
                Texts = (entry.Texts is null || filler.Texts is null) ? entry.Texts ?? filler.Texts : filler.Texts.Concat(entry.Texts).ToList(),
                Dividers = (entry.Dividers is null || filler.Dividers is null) ? entry.Dividers ?? filler.Dividers : filler.Dividers.Concat(entry.Dividers).ToList(),
            };
        }
    }
}
