using StardewValley;
using StardewValley.Delegates;

namespace DialogueDisplayFramework.Framework
{
    internal class DialogueGameStateQueries
    {
        internal static void Register()
        {
            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_NAME", (string[] query, GameStateQueryContext context) =>
            {
                if (!ArgUtility.TryGet(query, 1, out string name, out string error))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error);
                }

                return Game1.currentSpeaker?.Name.ToString().ToLower() == name.ToLower();
            });

            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_GENDER", (string[] query, GameStateQueryContext context) =>
            {
                if (!ArgUtility.TryGet(query, 1, out string gender, out string error))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error);
                }

                return Game1.currentSpeaker?.Gender.ToString().ToLower() == gender.ToLower();
            });

            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_CAN_BE_ROMANCED", (string[] query, GameStateQueryContext context) =>
            {
                return Game1.currentSpeaker?.datable.Value == true;
            });

            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_CAN_SOCIALIZE", (string[] query, GameStateQueryContext context) =>
            {
                return Game1.currentSpeaker?.CanSocialize == true;
            });

            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_CAN_RECEIVE_GIFTS", (string[] query, GameStateQueryContext context) =>
            {
                return Game1.currentSpeaker?.CanReceiveGifts() == true;
            });

            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_APPEARANCE_ID", (string[] query, GameStateQueryContext context) =>
            {
                if (!ArgUtility.TryGet(query, 1, out string appearanceid, out string error))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error);
                }

                return Game1.currentSpeaker?.LastAppearanceId?.ToLower() == appearanceid.ToLower();
            });

            GameStateQuery.Register("Mangupix.DDFC_SPEAKER_FRIENDSHIP_POINTS", (string[] query, GameStateQueryContext context) =>
            {
                if (!ArgUtility.TryGetInt(query, 1, out var minPoints, out string error, "int minPoints") || !ArgUtility.TryGetOptionalInt(query, 2, out var maxPoints, out error, int.MaxValue, "int maxPoints"))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error);
                }

                int friendshipLevelForNPC = Game1.player.getFriendshipLevelForNPC(Game1.currentSpeaker.Name);
                return friendshipLevelForNPC >= minPoints && friendshipLevelForNPC <= maxPoints;
            });
        }
    }
}
