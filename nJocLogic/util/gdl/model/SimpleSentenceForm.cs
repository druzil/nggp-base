using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// Represents a Fact template with extra information about total columns and access to inner Facts (TermFunctions)
    /// e.g. (FactName _ (FunctionName _ _ ))
    /// </summary>
    public class SimpleSentenceForm : AbstractSentenceForm
    {
        /// <summary>
        /// The arity is the same as the arity of the Fact object, i.e. how many terms there are at the first level (including functions).
        /// </summary>
        private readonly int _arity;

        //We cheat a little by reusing sentence forms as function forms. Dictionary from the index (< arity) to the function definition.
        private readonly ReadOnlyDictionary<int, SimpleSentenceForm> _functions;

        /// <summary>
        /// The tuple size is the total number of constants and/or variables within the entire sentence, including inside functions. 
        /// </summary>
        public override sealed int TupleSize { get; protected set; }

        public override sealed int Name { get; protected set; }

        public SimpleSentenceForm(Fact sentence) : this(sentence.RelationName, sentence.Arity, sentence.GetTerms()) { }

        private SimpleSentenceForm(TermFunction function) : this(function.FunctionName, function.Arity, function.Arguments) { }

        private SimpleSentenceForm(int name, int arity, IReadOnlyList<Term> terms)
        {
            TupleSize = 0;
            var functions = new Dictionary<int, SimpleSentenceForm>();
            for (int i = 0; i < arity; i++)
            {
                var term = terms[i] as TermFunction;
                if (term != null)
                {
                    var functionForm = new SimpleSentenceForm(term);
                    functions[i] = functionForm;
                    TupleSize += functionForm.TupleSize;
                }
                else
                    TupleSize++;
            }
            _arity = arity;
            Name = name;
            _functions = new ReadOnlyDictionary<int, SimpleSentenceForm>(functions);
        }

        private SimpleSentenceForm(int name, int arity, ReadOnlyDictionary<int, SimpleSentenceForm> functions, int tupleSize)
        {
            _arity = arity;
            _functions = functions;
            Name = name;
            TupleSize = tupleSize;
        }

        public override ISentenceForm WithName(int newName)
        {
            return new SimpleSentenceForm(newName, _arity, _functions, TupleSize);
        }

        /// <summary>
        /// Returns true iff the given sentence is of this sentence form.
        /// </summary>
        public override bool Matches(Fact sentence)
        {
            if (sentence.RelationName != Name || sentence.Arity != _arity)
                return false;

            for (int i = 0; i < sentence.Arity; i++)
            {
                var term = sentence.GetTerm(i) as TermFunction;
                if (term == null && _functions.ContainsKey(i))
                    return false;

                if (term != null)
                {
                    if (!_functions.ContainsKey(i) || !_functions[i].Matches(term))
                        return false;
                }
            }
            return true;
        }

        private bool Matches(TermFunction function)
        {
            if (function.FunctionName != Name || function.Arity != _arity)
                return false;

            for (int i = 0; i < function.Arity; i++)
            {
                var term = function.GetTerm(i) as TermFunction;
                if (_functions.ContainsKey(i) && term == null)
                    return false;

                if (term != null)
                    if (!_functions.ContainsKey(i) || !_functions[i].Matches(term))
                        return false;
            }
            return true;
        }

        public override Fact GetSentenceFromTuple(IList<Term> tuple)
        {
            if (tuple.Count != TupleSize)
                throw new Exception(string.Format("Passed tuple of the wrong size to a sentence form: tuple was {0}, sentence form is {1}", tuple, this));
            if (tuple.Count < _arity)
                throw new Exception(string.Format("Something is very wrong, probably fixable by the GdlCleaner; name: {0}; arity: {1}; tupleSize: {2}", Name, _arity, TupleSize));
            var sentenceBody = new List<Term>();
            int curIndex = 0;
            for (int i = 0; i < _arity; i++)
            {
                Term term = tuple[curIndex];
                Debug.Assert(!(term is TermFunction));
                if (_functions.ContainsKey(i))
                {
                    SimpleSentenceForm functionForm = _functions[i];
                    sentenceBody.Add(functionForm.GetFunctionFromTuple(tuple, curIndex));
                    curIndex += functionForm.TupleSize;
                }
                else
                {
                    sentenceBody.Add(term);
                    curIndex++;
                }
            }

            return _arity == 0 ? new VariableFact(true, Name) : new VariableFact(true, Name, sentenceBody.ToArray());
        }

        private TermFunction GetFunctionFromTuple(IList<Term> tuple, int curIndex)
        {
            var functionBody = new List<Term>();
            for (int i = 0; i < _arity; i++)
            {
                Term term = tuple[curIndex];
                Debug.Assert(!(term is TermFunction));
                if (_functions.ContainsKey(i))
                {
                    SimpleSentenceForm functionForm = _functions[i];
                    functionBody.Add(functionForm.GetFunctionFromTuple(tuple, curIndex));
                    curIndex += functionForm.TupleSize;
                }
                else
                {
                    functionBody.Add(term);
                    curIndex++;
                }
            }
            return new TermFunction(Name, functionBody.ToArray());
        }
    }
}
