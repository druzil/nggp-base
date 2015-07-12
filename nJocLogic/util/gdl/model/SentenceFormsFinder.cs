using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Wintellect.PowerCollections;

namespace nJocLogic.util.gdl.model
{
    using data;
    using gameContainer;

    /// <summary>
    /// Finds sentence forms from a game description and all the possible values for each term in each
    /// sentence form.
    /// </summary>
    public class SentenceFormsFinder
    {
        private bool _haveCreatedModel;
        private readonly IList<Expression> _description;
        private SentenceModel _sentencesModel;

        public SentenceFormsFinder(IList<Expression> description)
        {
            _description = description;
        }

        /// <summary>
        /// Will return all possilbe forms that the descriptions can take  e.g.
        /// <para/>...(next (cell _ _ _))
        /// <para/>(next (control _))
        /// <para/>(legal _ _)
        /// <para/>(legal _ (mark _ _))...
        /// </summary>
        /// <returns></returns>
        public ImmutableHashSet<ISentenceForm> FindSentenceForms()
        {
            CreateModel();
            return GetSentenceFormsFromModel().ToImmutableHashSet();
        }

        /// <summary>
        /// For each sentence form (e.g. (legal _ _))
        /// <para />find the possible constants that can be bound to each column of the sentence
        /// </summary>
        /// <returns></returns>
        public Dictionary<ISentenceForm, ISentenceFormDomain> FindCartesianDomains()
        {
            CreateModel();
            return GetCartesianDomainsFromModel();
        }

        private void CreateModel()
        {
            lock (this)
            {
                if (!_haveCreatedModel)
                {
                    _sentencesModel = new SentenceModel(_description);
                    _haveCreatedModel = true;
                }
            }
        }

        private Dictionary<ISentenceForm, ISentenceFormDomain> GetCartesianDomainsFromModel()
        {
            var results = new Dictionary<ISentenceForm, ISentenceFormDomain>();
            foreach (NameAndArity sentenceEntry in _sentencesModel.SentencesModel.Keys)
            {
                List<TermModel> bodyModels = _sentencesModel.SentencesModel[sentenceEntry];
                // We'll end up taking the Cartesian product of the different types of terms we have available
                if (sentenceEntry.Arity == 0)
                {
                    Fact sentence = new GroundFact(sentenceEntry.Name);
                    var form = new SimpleSentenceForm(sentence);
                    results[form] = new CartesianSentenceFormDomain(form, new MultiDictionary<int, TermObject>(false));
                }
                else
                {
                    IEnumerable<HashSet<Term>> sampleTerms = ToSampleTerms(bodyModels);
                    foreach (IEnumerable<Term> terms in sampleTerms.CartesianProduct())
                    {
                        Fact sentence = new VariableFact(true, sentenceEntry.Name, terms.ToArray());
                        var form = new SimpleSentenceForm(sentence);
                        results[form] = GetDomain(form, sentence);
                    }
                }
            }
            return results;
        }

        private CartesianSentenceFormDomain GetDomain(ISentenceForm form, Fact sentence)
        {
            var domainContents = new List<ISet<TermObject>>();
            GetDomainInternal(sentence.GetTerms(), _sentencesModel.GetBodyModel(sentence), domainContents);
            return new CartesianSentenceFormDomain(form, domainContents);
        }

        private static void GetDomainInternal(IList<Term> body, IList<TermModel> bodyModel, ICollection<ISet<TermObject>> domainContents)
        {
            if (body.Count != bodyModel.Count)
                throw new Exception("Should have same arity in example as in model");

            for (int i = 0; i < body.Count; i++)
            {
                Term term = body[i];
                TermModel termModel = bodyModel[i];
                if (term is TermObject)
                    domainContents.Add(termModel.PossibleConstants);
                else
                {
                    var function = (TermFunction)term;                          //if this cast is invalid something has gone wrong
                    List<TermModel> functionBodyModel = termModel.GetFunctionBodyModel(function);
                    GetDomainInternal(function.Arguments, functionBodyModel, domainContents);
                }
            }
        }

        private IEnumerable<ISentenceForm> GetSentenceFormsFromModel()
        {
            var results = new HashSet<ISentenceForm>();
            foreach (NameAndArity sentenceEntry in _sentencesModel.SentencesModel.Keys)
            {
                int name = sentenceEntry.Name;
                List<TermModel> bodyModels = _sentencesModel.SentencesModel[sentenceEntry];
                // We'll end up taking the Cartesian product of the different types of terms we have available
                if (sentenceEntry.Arity == 0)
                    results.Add(new SimpleSentenceForm(new VariableFact(false, name)));
                else
                {
                    var cartesianProduct = ToSampleTerms(bodyModels).CartesianProduct();
                    foreach (IEnumerable<Term> terms in cartesianProduct)
                        results.Add(new SimpleSentenceForm(new VariableFact(true, name, terms.ToArray())));
                }
            }
            return results;
        }

        private static IEnumerable<HashSet<Term>> ToSampleTerms(IEnumerable<TermModel> bodyModels)
        {
            return bodyModels.Select(ToSampleTerms);
        }

        private static HashSet<Term> ToSampleTerms(TermModel termModel)
        {
            var results = new HashSet<Term>();
            if (termModel.PossibleConstants.Any())
                results.Add(termModel.PossibleConstants.First());

            foreach (NameAndArity nameAndArity in termModel.PossibleFunctions.Keys)
            {
                List<TermModel> bodyModel = termModel.PossibleFunctions[nameAndArity];
                IEnumerable<HashSet<Term>> functionSampleTerms = ToSampleTerms(bodyModel);
                var functionBodies = functionSampleTerms.CartesianProduct();
                foreach (IEnumerable<Term> functionBody in functionBodies)
                    results.Add(new TermFunction(nameAndArity.Name, functionBody.ToArray()));
            }
            return results;
        }
    }

    /// <summary>
    /// A container for a collection of term models for all expressions in the description
    /// </summary>
    internal class SentenceModel
    {
        internal readonly ModelFunctions SentencesModel = new ModelFunctions();

        internal SentenceModel(IList<Expression> description)
        {
            AddTrueSentencesToModel(description);
            ApplyRulesToModel(description);
        }

        private void AddTrueSentencesToModel(IEnumerable<Expression> description)
        {
            foreach (Fact gdl in description.OfType<Fact>())
                AddSentenceToModel(gdl, ImmutableDictionary.Create<TermVariable, TermModel>());
        }

        private void ApplyRulesToModel(IList<Expression> description)
        {
            bool changeMade = true;
            while (changeMade)
            {
                changeMade = description.OfType<Implication>().Aggregate(false, (current, gdl) => current | AddRule(gdl));
                changeMade |= ApplyLanguageRules();
            }
        }

        public List<TermModel> GetBodyModel(Fact sentence)
        {
            return SentencesModel[new NameAndArity(sentence)];
        }

        private bool ApplyInjection(NameAndArity oldName, NameAndArity newName)
        {
            Debug.Assert(oldName.Arity == newName.Arity);
            bool changesMade = false;
            if (SentencesModel.ContainsKey(oldName))
            {
                List<TermModel> oldModel = SentencesModel[oldName];
                changesMade = SentencesModel.CreateIfRequired(newName);
                List<TermModel> newModel = SentencesModel[newName];
                if (oldModel.Count != newModel.Count)
                    throw new Exception();
                for (int i = 0; i < oldModel.Count; i++)
                    changesMade |= newModel[i].MergeIn(oldModel[i]);
            }
            return changesMade;
        }

        private bool AddSentenceToModel(Fact sentence, IDictionary<TermVariable, TermModel> varsToModelsMap)
        {
            var sentenceName = new NameAndArity(sentence);
            bool changesMade = SentencesModel.CreateIfRequired(sentenceName);
            return changesMade | TermModel.AddBodyToModel(SentencesModel[sentenceName], sentence.GetTerms(), varsToModelsMap);
        }

        private bool AddRule(Implication rule)
        {
            // Stuff can make it into the head sentence form either as part of the head of the rule as presented or due to a
            // variable connected to the positive literals in the rule. (In the latter case, it should be in the intersection 
            // of the models of all such positive literals.) For each slot in the body, we want to set up everything that will
            // be injected into it.
            Fact headSentence = rule.Consequent;

            // We need to get the possible contents of variables beforehand, to deal with the case of variables being inside functions.
            Dictionary<TermVariable, TermModel> varsToModelsMap = GetVarsToModelsMap(rule);

            return AddSentenceToModel(headSentence, varsToModelsMap);
        }

        private Dictionary<TermVariable, TermModel> GetVarsToModelsMap(Implication rule)
        {
            var varsToUse = new HashSet<TermVariable>(rule.Consequent.VariablesOrEmpty);
            var varsToModelsMap = new Dictionary<TermVariable, TermModel>();
            foreach (TermVariable var in varsToUse)
                varsToModelsMap[var] = new TermModel();

            foreach (Expression literal in rule.Antecedents.Conjuncts)
            {
                var fact = literal as Fact;
                if (fact != null)
                {
                    List<Term> literalBody = fact.GetTerms();
                    var nameAndArity = new NameAndArity(fact);
                    SentencesModel.CreateIfRequired(nameAndArity);
                    List<TermModel> literalModel = SentencesModel[nameAndArity];
                    AddVariablesToMap(literalBody, literalModel, varsToModelsMap);
                }
            }
            return varsToModelsMap;
        }

        private static void AddVariablesToMap(IList<Term> body, IList<TermModel> model,
                        IDictionary<TermVariable, TermModel> varsToModelsMap)
        {
            if (body.Count != model.Count)
                throw new ArgumentException(string.Format("The term model and body sizes don't match: model is {0}, body is: {1}", model, body));

            for (int i = 0; i < body.Count; i++)
            {
                Term term = body[i];
                TermModel termModel = model[i];
                var variable = term as TermVariable;
                if (variable == null)
                {
                    var function = term as TermFunction;
                    if (function != null)
                    {
                        List<TermModel> functionBodyModel = termModel.GetFunctionBodyModel(function);
                        if (functionBodyModel != null)
                            AddVariablesToMap(function.Arguments.ToList(), functionBodyModel, varsToModelsMap);
                    }
                }
                else
                {
                    if (varsToModelsMap.ContainsKey(variable))
                        varsToModelsMap[variable].MergeIn(termModel);
                }
            }
        }

        private bool ApplyLanguageRules()
        {
            bool changesMade = ApplyInjection(new NameAndArity(GameContainer.Parser.TokInit, 1),
                                          new NameAndArity(GameContainer.Parser.TokTrue, 1));
            changesMade |= ApplyInjection(new NameAndArity(GameContainer.Parser.TokNext, 1),
                                          new NameAndArity(GameContainer.Parser.TokTrue, 1));
            changesMade |= ApplyInjection(new NameAndArity(GameContainer.Parser.TokLegal, 2),
                                          new NameAndArity(GameContainer.Parser.TokDoes, 2));
            return changesMade;
        }
    }

    /// <summary> 
    /// Represents a placeholder for possible functions or constants. The possible functions can contain further TermModels 
    /// </summary>
    internal class TermModel
    {
        public HashSet<TermObject> PossibleConstants { get; private set; }
        public ModelFunctions PossibleFunctions { get; private set; }

        public TermModel()
        {
            PossibleFunctions = new ModelFunctions();
            PossibleConstants = new HashSet<TermObject>();
        }

        public List<TermModel> GetFunctionBodyModel(TermFunction function)
        {
            return PossibleFunctions.GetValueOrNull(new NameAndArity(function));
        }

        public bool MergeIn(TermModel other)
        {
            bool changesMade = !PossibleConstants.IsSupersetOf(other.PossibleConstants);
            PossibleConstants.UnionWith(other.PossibleConstants);
            foreach (NameAndArity key in other.PossibleFunctions.Keys)
            {
                List<TermModel> theirFunctionBodies = other.PossibleFunctions[key];
                if (PossibleFunctions.ContainsKey(key))
                {
                    List<TermModel> ourFunctionBodies = PossibleFunctions[key];
                    if (ourFunctionBodies.Count != theirFunctionBodies.Count)
                        throw new Exception();

                    for (int i = 0; i < ourFunctionBodies.Count; i++)
                        changesMade |= ourFunctionBodies[i].MergeIn(theirFunctionBodies[i]);
                }
                else
                {
                    PossibleFunctions[key] = DeepCopyOf(theirFunctionBodies);
                    changesMade = true;
                }
            }
            return changesMade;
        }

        public bool AddTerm(Term term, IDictionary<TermVariable, TermModel> varsToModelsMap)
        {
            var termObject = term as TermObject;
            if (termObject != null)
                return PossibleConstants.Add(termObject);

            var function = term as TermFunction;
            if (function != null)
            {
                var sentenceName = new NameAndArity(function);
                bool changesMade = PossibleFunctions.CreateIfRequired(sentenceName);
                return changesMade | AddBodyToModel(PossibleFunctions[sentenceName], function.Arguments.ToList(), varsToModelsMap);
            }
            var key = term as TermVariable;
            if (key != null)
                return MergeIn(varsToModelsMap[key]);

            throw new Exception(String.Format("Unrecognized term type {0} for term {1}", term.GetType(), term));
        }

        public override String ToString()
        {
            string constants = String.Join(",", PossibleConstants);
            string functions = String.Join(",", PossibleFunctions.Keys);
            return String.Format("NewTermModel [possibleConstants={0}, possibleFunctions={1}]", constants, functions);
        }

        private static TermModel CopyOf(TermModel originalTermModel)
        {
            var termModel = new TermModel();
            termModel.MergeIn(originalTermModel);
            return termModel;
        }

        private static List<TermModel> DeepCopyOf(IEnumerable<TermModel> original)
        {
            return original.Select(CopyOf).ToList();
        }

        internal static bool AddBodyToModel(IList<TermModel> model, IList<Term> body, IDictionary<TermVariable, TermModel> varsToModelsMap)
        {
            bool changesMade = false;
            if (model.Count != body.Count)
                throw new Exception(String.Format("The term model and body sizes don't match: model is {0}, body is: {1}", model, body));

            for (int i = 0; i < model.Count; i++)
            {
                TermModel termModel = model[i];
                Term term = body[i];
                changesMade |= termModel.AddTerm(term, varsToModelsMap);
            }
            return changesMade;
        }
    }

    /// <summary>
    /// A list of functions and their columns with functions/objects that can be assigned
    /// </summary>
    internal class ModelFunctions
    {
        private readonly Dictionary<NameAndArity, List<TermModel>> _functions;

        public ModelFunctions()
        {
            _functions = new Dictionary<NameAndArity, List<TermModel>>();
        }

        public IEnumerable<NameAndArity> Keys { get { return _functions.Keys; } }

        public List<TermModel> GetValueOrNull(NameAndArity function)
        {
            List<TermModel> result;
            _functions.TryGetValue(function, out result);
            return result;
        }

        public List<TermModel> this[NameAndArity sentenceName]
        {
            get { return _functions[sentenceName]; }
            set { _functions[sentenceName] = value; }
        }

        public bool CreateIfRequired(NameAndArity sentenceName)
        {
            if (_functions.ContainsKey(sentenceName))
                return false;

            var result = new List<TermModel>(sentenceName.Arity);
            for (int i = 0; i < sentenceName.Arity; i++)
                result.Add(new TermModel());
            _functions[sentenceName] = result;
            return true;
        }

        public bool ContainsKey(NameAndArity nameAndArity)
        {
            return _functions.ContainsKey(nameAndArity);
        }
    }

    class NameAndArity
    {
        internal int Name { get; private set; }
        internal int Arity { get; private set; }

        public NameAndArity(Fact sentence)
        {
            Name = sentence.RelationName;
            Arity = sentence.Arity;
        }

        public NameAndArity(TermFunction function)
        {
            Name = function.FunctionName;
            Arity = function.Arity;
        }

        public NameAndArity(int name, int arity)
        {
            Name = name;
            Arity = arity;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + Arity;
            result = prime * result + Name;
            return result;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            var other = (NameAndArity)obj;
            return Arity == other.Arity && Name.Equals(other.Name);
        }

        public override String ToString()
        {
            string name = GameContainer.SymbolTable[Name];
            return string.Format("NameAndArity [name={0}, arity={1}]", name, Arity);
        }
    }
}
