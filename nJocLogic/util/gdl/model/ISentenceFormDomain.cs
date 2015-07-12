using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// A SentenceFormDomain contains information about the possible
    /// sentences of a particular sentence form within a game. In other
    /// words, it captures information about which constants can be
    /// in which positions in the SentenceForm.</summary>
    public interface ISentenceFormDomain : IEnumerable<Fact>
    {
        /// <summary>
        /// Returns the SentenceForm associated with this domain.
        /// </summary>
        /// <value></value>
        ISentenceForm Form { get; }

        /// <summary>
        /// Returns a set containing every constant that can appear in the given slot index in the sentence form.
        /// </summary>
        /// <param name="slotIndex"></param>
        /// <returns></returns>
        ISet<TermObject> GetDomainForSlot(int slotIndex);
    }
}
