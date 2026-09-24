namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Tracks the animation state for one NPC during dialogue.
    /// Handles expression switching, frame cycling, and all animation modes.
    /// </summary>
    public class AnimationState
    {
        public PortraitDefinition Portrait { get; }

        /// <summary>Current expression index being tracked.</summary>
        public int CurrentExpression { get; private set; } = -1;

        /// <summary>Current frame within the expression's animation (0-based).</summary>
        public int CurrentFrame { get; private set; }

        /// <summary>Milliseconds accumulated since last frame change.</summary>
        public double ElapsedMs { get; private set; }

        /// <summary>Whether a one-shot animation has finished playing.</summary>
        public bool OneShotFinished { get; private set; }

        private ExpressionDefinition activeExpr;

        /// <summary>When set, overrides normal expression resolution (used for body-change animations).</summary>
        public ExpressionDefinition BodyChangeOverride { get; set; }

        public AnimationState(PortraitDefinition portrait)
        {
            this.Portrait = portrait;
        }

        /// <summary>Whether the current expression has an active animation config.</summary>
        public bool IsActive => this.activeExpr != null && !this.OneShotFinished;

        /// <summary>Returns the active expression definition (may be variant-specific).</summary>
        public ExpressionDefinition ActiveExpression => this.activeExpr;

        /// <summary>The variant that was active when the expression was last set.</summary>
        private string activeVariant = "";

        /// <summary>Sets the active expression. Resets animation if expression changed or variant changed.</summary>
        public void SetExpression(int expressionIndex, string variant = "")
        {
            // Body-change override takes precedence over normal expression resolution
            if (this.BodyChangeOverride != null)
            {
                if (this.activeExpr != this.BodyChangeOverride)
                {
                    this.activeExpr = this.BodyChangeOverride;
                    this.CurrentFrame = 0;
                    this.ElapsedMs = 0;
                    this.OneShotFinished = false;
                }
                return;
            }

            variant ??= "";
            if (this.CurrentExpression == expressionIndex && this.activeVariant == variant)
                return;

            this.CurrentExpression = expressionIndex;
            this.activeVariant = variant;
            string key = expressionIndex.ToString();

            // Try variant-specific expression config first, fall back to default
            this.activeExpr = null;
            if (!string.IsNullOrEmpty(variant) && this.Portrait.VariantExpressions.Count > 0)
            {
                this.activeExpr = ResolveVariantExpression(key, variant);
            }
            this.activeExpr ??= this.Portrait.Expressions.ContainsKey(key)
                ? this.Portrait.Expressions[key]
                : null;

            this.CurrentFrame = 0;
            this.ElapsedMs = 0;
            this.OneShotFinished = false;
        }

        /// <summary>
        /// Resolves a variant expression definition with fallback chain:
        /// full combined variant → context-only → hearts-only.
        /// </summary>
        private ExpressionDefinition ResolveVariantExpression(string exprKey, string variant)
        {
            // Try full variant name (e.g. "Summer_Hearts4" or "Hearts2")
            if (this.Portrait.VariantExpressions.TryGetValue(variant, out var exprMap)
                && exprMap.TryGetValue(exprKey, out var expr))
                return expr;

            // If combined variant, try context-only then hearts-only
            int sep = variant.IndexOf('_');
            if (sep > 0)
            {
                string contextPart = variant.Substring(0, sep);
                string heartsPart = variant.Substring(sep + 1);

                if (this.Portrait.VariantExpressions.TryGetValue(contextPart, out exprMap)
                    && exprMap.TryGetValue(exprKey, out expr))
                    return expr;

                if (this.Portrait.VariantExpressions.TryGetValue(heartsPart, out exprMap)
                    && exprMap.TryGetValue(exprKey, out expr))
                    return expr;
            }

            return null;
        }

        /// <summary>Advances the animation timer. Returns true if frame changed.</summary>
        public bool Update(double elapsedMs)
        {
            if (this.activeExpr == null || this.OneShotFinished)
                return false;

            this.ElapsedMs += elapsedMs;

            if (this.ElapsedMs < this.activeExpr.FrameDelayMs)
                return false;

            this.ElapsedMs -= this.activeExpr.FrameDelayMs;

            int nextFrame = this.CurrentFrame + 1;
            string mode = this.activeExpr.Mode?.ToLowerInvariant() ?? "loop";

            switch (mode)
            {
                case "loop":
                case "pingpong":
                    // Infinite cycling
                    this.CurrentFrame = nextFrame % this.activeExpr.TotalFrames;
                    break;

                case "once":
                    if (nextFrame >= this.activeExpr.TotalFrames)
                    {
                        this.CurrentFrame = this.activeExpr.TotalFrames - 1;
                        this.OneShotFinished = true;
                    }
                    else
                        this.CurrentFrame = nextFrame;
                    break;

                case "once-pingpong":
                    if (nextFrame >= this.activeExpr.TotalFrames)
                    {
                        this.CurrentFrame = 0;
                        this.OneShotFinished = true;
                    }
                    else
                        this.CurrentFrame = nextFrame;
                    break;

                default:
                    this.CurrentFrame = nextFrame % this.activeExpr.TotalFrames;
                    break;
            }

            return true;
        }

        /// <summary>
        /// Computes the starting row in the grid for a given expression index,
        /// by summing up the rows used by all expressions with lower indices.
        /// Supports unlimited expressions — iterates from 0 up to the requested index.
        /// </summary>
        public int GetStartRow(int expressionIndex)
        {
            int rowOffset = 0;
            for (int i = 0; i < expressionIndex; i++)
            {
                string key = i.ToString();
                if (this.Portrait.Expressions.TryGetValue(key, out var expr))
                {
                    int rowsForExpr = (expr.TotalFrames + this.Portrait.Columns - 1) / this.Portrait.Columns;
                    rowOffset += rowsForExpr;
                }
            }
            return rowOffset;
        }

        /// <summary>
        /// Gets the absolute portrait index (grid cell number) for the current animation state.
        /// This index is what Stardew uses to pick the source rectangle from the portrait texture.
        /// </summary>
        public int GetAbsoluteFrameIndex()
        {
            if (this.activeExpr == null || this.CurrentExpression < 0)
                return 0;

            int startRow = this.GetStartRow(this.CurrentExpression);
            int columns = this.Portrait.Columns;

            int frameCol = this.CurrentFrame % columns;
            int frameRow = this.CurrentFrame / columns;

            int absoluteRow = startRow + frameRow;
            return absoluteRow * columns + frameCol;
        }

        /// <summary>Fully resets animation state for a new dialogue session.</summary>
        public void Reset()
        {
            this.CurrentFrame = 0;
            this.ElapsedMs = 0;
            this.OneShotFinished = false;
            this.CurrentExpression = -1;
            this.activeExpr = null;
            this.BodyChangeOverride = null;
        }
    }
}
