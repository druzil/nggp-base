namespace nJocLogic.util.gdl.GdlCleaner
{
    using System.Collections.Generic;
    using System.Linq;
    using data;

    /// <summary>
    /// Cleans up various issues with games to make them more standardized.
    /// <para/>- removes zero element bodies (converts implications that have no antecents to a fact)
    /// <para/>- removes empty terms
    /// <para/>- removes not distincts that have literals in them
    /// <para/>- removes 'base' expressions that are of the old style
    /// </summary>
    public static class GdlCleaner
    {
        private const int MaxIterations = 100;

        public static IList<Expression> Run(IList<Expression> description)
        {
            for (int i = 0; i < MaxIterations; i++)
            {
                List<Expression> newDescription = RunOnce(description);
                if (newDescription.SequenceEqual(description))
                    break;
                description = newDescription;
            }
            return description;
        }

        private static List<Expression> RunOnce(IEnumerable<Expression> description)
        {
            //TODO: Add (role ?player) where appropriate, i.e. in rules for "legal" or "input" where the first argument is an undefined variable
            //TODO: Get rid of GdlPropositions in the description
            //TODO: Expand to functions

            List<Expression> newDescription = ZeroElementBodyRemover.Run(description);
            newDescription = ExtraBracketRemover.Run(newDescription);
            newDescription = NotDistinctLiteralRemover.Run(newDescription);
            //newDescription = DistinctSorter.Run(newDescription); INFO: is currently called from MetaGdl
            return OldBaseSentenceRemover.Run(newDescription);
        }
    }
}
