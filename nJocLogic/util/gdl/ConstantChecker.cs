using System.Collections.Generic;
using nJocLogic.data;
using nJocLogic.util.gdl.model;

namespace nJocLogic.util.gdl
{
    ///<summary>
    /// A ConstantChecker provides information about which sentences are true for the constant sentence forms in a game. 
    /// These can be computed once at the beginning of a match, to avoid redundant computations.
    ///
    /// The preferred way to create a ConstantChecker is with
    /// {@link ConstantCheckerFactory#createWithForwardChaining(org.ggp.base.util.gdl.model.SentenceDomainModel)}.
    ///</summary>
    public interface IConstantChecker
    {
        ///<summary>
        /// Returns true iff the sentence is of a constant form included in this ConstantChecker.
        ///</summary>
        bool HasConstantForm(Fact sentence);

        ///<summary>
        /// Returns true iff the given sentence form is constant and is included in this ConstantChecker.
        /// A constant sentence doesn't depend on 'true' or 'does'
        ///</summary>
        bool IsConstantForm(ISentenceForm form);

        ///<summary>
        /// Returns the set of all true sentences of the given constant sentence form.
        ///</summary>
        ISet<Fact> GetTrueSentences(ISentenceForm form);

        ///<summary>
        /// Returns the set of all constant sentence forms included in this ConstantChecker.
        ///</summary>
        ISet<ISentenceForm> ConstantSentenceForms { get; }

        ///<summary>
        /// Returns true iff the given sentence is of a constant sentence form and is always true.
        ///</summary>
        bool IsTrueConstant(Fact sentence);

        ///<summary>
        /// Returns the sentence form model that the constant checker is based on.
        ///</summary>
        ISentenceFormModel SentenceFormModel { get; }
    }
}
