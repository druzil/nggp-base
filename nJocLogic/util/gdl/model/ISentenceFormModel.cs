namespace nJocLogic.util.gdl.model
{
    using System.Collections.Generic;
    using data;

    ///<summary>
    /// A model of the different types of sentences that may be true over
    /// the course of a game.
    /// <para />
    /// The SentenceFormModel uses the notion of a sentence form. This defines
    /// the name of a sentence and the structure of functions within the
    /// sentence.
    /// <para />
    /// The recommended way of creating a SentenceFormModel is via
    /// <see cref="SentenceFormModelFactory.Create"/>
    ///</summary>
    public interface ISentenceFormModel
    {
        ///<summary>
        /// Returns the set of sentence forms that are independent; that is,
        /// the truth values of the sentences of these forms may depend on
        /// the turn of the game, but never on players' actions.
        ///
        /// For example, in tic-tac-toe, the sentence form (true (control _))
        /// is independent, but not constant: it changes from turn to turn,
        /// but always in the same way.
        ///
        /// All constant sentence forms are independent, so this is a superset
        /// of <see cref="get_ConstantSentenceForms"/>
        ///</summary>
        ISet<ISentenceForm> IndependentSentenceForms { get; }

        ///<summary>
        /// Returns the set of sentence forms that are constant; that is,
        /// the truth values of sentences of these forms do not change
        /// over the course of the game.
        ///
        /// The values of these sentences may be precomputed using a
        /// <see cref="IConstantChecker"/>
        ///</summary>
        ISet<ISentenceForm> ConstantSentenceForms { get; }

        ///<summary>
        /// Returns a graph describing how the sentence forms relate to one
        /// another in the rules of the game. One sentence form depends on
        /// another if a rule producing the first sentence form has the
        /// second sentence form in its body.
        ///
        /// Each key depends on the sentence forms in the associated collection.
        ///
        /// Note that this graph structure may contain cycles, and a sentence form
        /// may depend on itself. Consider using
        /// <see cref="DependencyGraphs.ToposortSafe{T}"/> to obtain a
        /// topological ordering in a way that respects cycles.
        ///</summary>
        IDictionary<ISentenceForm, ICollection<ISentenceForm>> DependencyGraph { get; }

        ///<summary>
        /// Returns the list of sentences specifically listed as true in the
        /// game description for that sentence form.
        ///</summary>
        ISet<Fact> GetSentencesListedAsTrue(ISentenceForm form);

        ///<summary>
        /// Returns the rules that GENERATE the sentence form, not necessarily
        /// all the rules that contain it.
        ///
        /// Note that if functions can be assigned to variables, this might not
        /// find all the rules capable of generating sentences of the given form.
        ///</summary>
        ISet<Implication> GetRules(ISentenceForm form);

        ///<summary>
        /// Returns all sentence forms in the model.
        ///</summary>
        ISet<ISentenceForm> SentenceForms { get; }

        ///<summary>
        /// Returns the sentence form of the given sentence.
        ///</summary>
        ISentenceForm GetSentenceForm(Fact transformed);

        ///<summary>
        /// Returns the game description for the game.
        ///</summary>
        IList<Expression> Description { get; }
    }
}
