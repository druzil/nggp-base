using System.Collections.Generic;

namespace nJocLogic.util.gdl.model
{    
    using data;

    /// <summary>
    /// A sentence form captures the structure of a group of possible <see cref="Expression"/>. Two sentences have 
    /// the same form if they have the same name and include the same functions in the same place
    /// 
    /// Implementations of SentenceForm should be immutable. They should extend <see cref="AbstractSentenceForm"/> 
    /// for implementations of hashCode and equals that will be compatible with other SentenceForms, as well as a 
    /// recommended implementation of ToString.
    /// </summary>
    public interface ISentenceForm
    {
        /// <summary>
        /// Returns the name of all sentences with this form.
        /// </summary>
        int Name { get; }

        /// <summary>
        /// Returns a sentence form exactly like this one, except with a new name.
        /// </summary>
        ISentenceForm WithName(int name);

        /// <summary>
        /// Returns true iff the given sentence is of this sentence form.
        /// </summary>
        bool Matches(Fact relation);

        /// <summary>
        /// The tuple size is the total number of constants and/or variables within the entire sentence, including inside functions. 
        /// </summary>
        int TupleSize { get; }

        /// <summary>
        /// Given a list of GdlConstants and/or GdlVariables in the order they would appear in a sentence of this 
        /// sentence form, returns that sentence.
        /// 
        /// For the opposite operation (getting a tuple from a sentence), see 
        /// <see cref="GdlUtils#getTupleFromSentence(Fact)"/> and <see cref="GdlUtils#getTupleFromGroundSentence(Fact)"/>
        /// </summary>
        Fact GetSentenceFromTuple(IList<Term> tuple);
    }
}
