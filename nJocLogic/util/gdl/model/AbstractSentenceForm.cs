using System;
using System.Collections.Generic;
using nJocLogic.gameContainer;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// Defines the hashCode, equals, and toString methods for SentenceForms so different SentenceForms can be compatible in 
    /// terms of how they treat these methods. SentenceForm implementations should extend this class and should not 
    /// reimplement hashCode, equals, or toString.
    /// </summary>
    public abstract class AbstractSentenceForm : ISentenceForm
    {
        public override bool Equals(Object obj)
        {
            var sentenceForm = obj as AbstractSentenceForm;
            if (sentenceForm == null)
                return false;

            //return Name == sentenceForm.Name && TupleSize == sentenceForm.TupleSize && sentenceForm.Matches(UnderscoreSentence);
            return sentenceForm.UnderscoreSentence.Equals(UnderscoreSentence);
        }

        public abstract bool Matches(Fact relation);
        public abstract int TupleSize { get; protected set; }
        public abstract Fact GetSentenceFromTuple(IList<Term> terms);
        public abstract int Name { get; protected set; }
        public abstract ISentenceForm WithName(int name);

        private Fact UnderscoreSentence
        {
            get
            {
                if (_underscoreSentence==null)
                {
                    List<Term> underscores = GetNUnderscores(TupleSize);
                    _underscoreSentence = GetSentenceFromTuple(underscores);                      
                }              
                return _underscoreSentence;
            }
        }

        private static List<Term> GetNUnderscores(int numTerms)
        {
            int underscore = GameContainer.SymbolTable["_"];
            var terms = new List<Term>();
            for (var i = 0; i < numTerms; i++)
                terms.Add(TermObject.MakeTermObject(underscore));
            return terms;
        }

        private volatile int _hashCode;
        private Fact _underscoreSentence;

        public override int GetHashCode()
        {
            if (_hashCode == 0)
                //_hashCode = ToString().GetHashCode();
                _hashCode = UnderscoreSentence.GetHashCode();

            return _hashCode;
        }

        public override String ToString()
        {
            return UnderscoreSentence.ToString();
        }
    }
}
