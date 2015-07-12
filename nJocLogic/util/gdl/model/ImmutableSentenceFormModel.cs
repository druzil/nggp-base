using System.Collections.Generic;
using System.Collections.Immutable;
using nJocLogic.data;
using Wintellect.PowerCollections;
using System.Diagnostics;

namespace nJocLogic.util.gdl.model
{
    public class ImmutableSentenceFormModel : ISentenceFormModel
    {
        struct FactSignature
        {
            public int RelationName;
            public int Arity;

            public FactSignature(int relationName, int arity)
            {
                RelationName = relationName;
                Arity = arity;
            }
        }

        private readonly MultiDictionary<ISentenceForm, ISentenceForm> _dependencyGraph;
        private readonly MultiDictionary<ISentenceForm, Implication> _rulesByForm;
        private readonly MultiDictionary<ISentenceForm, Fact> _trueSentencesByForm;

        public ImmutableSentenceFormModel(ImmutableList<Expression> gameDescription,
                                          ImmutableHashSet<ISentenceForm> sentenceForms,
                                          ImmutableHashSet<ISentenceForm> constantSentenceForms,
                                          ImmutableHashSet<ISentenceForm> independentSentenceForms,
                                          MultiDictionary<ISentenceForm, ISentenceForm> dependencyGraph,
                                          MultiDictionary<ISentenceForm, Implication> rulesByForm,
                                          MultiDictionary<ISentenceForm, Fact> trueSentencesByForm)
        {
            Debug.Assert(sentenceForms.IsSupersetOf(independentSentenceForms));
            Debug.Assert(independentSentenceForms.IsSupersetOf(constantSentenceForms));
            Debug.Assert(sentenceForms.IsSupersetOf(dependencyGraph.Keys));
            Debug.Assert(sentenceForms.IsSupersetOf(dependencyGraph.Values));
            Debug.Assert(sentenceForms.IsSupersetOf(rulesByForm.Keys));
            Debug.Assert(sentenceForms.IsSupersetOf(trueSentencesByForm.Keys));
            Description = gameDescription;
            SentenceForms = sentenceForms;
            ConstantSentenceForms = constantSentenceForms;
            IndependentSentenceForms = independentSentenceForms;
            _dependencyGraph = dependencyGraph;
            _rulesByForm = rulesByForm;
            _trueSentencesByForm = trueSentencesByForm;
        }

        /// <summary>
        /// Returns an ImmutableSentenceFormModel with the same contents as the given SentenceFormModel.
        /// May not actually create a copy if the input is immutable.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public static ImmutableSentenceFormModel CopyOf(ISentenceFormModel other)
        {
            var immutableSentenceDomainModel = other as ImmutableSentenceDomainModel;
            if (immutableSentenceDomainModel != null)
                return CopyOf(immutableSentenceDomainModel.FormModel);

            var immutableSentenceFormModel = other as ImmutableSentenceFormModel;
            if (immutableSentenceFormModel != null)
                return immutableSentenceFormModel;

            var rulesByForm = new MultiDictionary<ISentenceForm, Implication>(false);
            var trueSentencesByForm = new MultiDictionary<ISentenceForm, Fact>(false);
            foreach (ISentenceForm form in other.SentenceForms)
            {
                rulesByForm[form] = other.GetRules(form);
                trueSentencesByForm[form] = other.GetSentencesListedAsTrue(form);
            }

            var dependencyGraph = new MultiDictionary<ISentenceForm, ISentenceForm>(true);
            foreach (KeyValuePair<ISentenceForm, ICollection<ISentenceForm>> kv in other.DependencyGraph)
                dependencyGraph[kv.Key] = kv.Value;

            return new ImmutableSentenceFormModel(ImmutableList.CreateRange(other.Description),
                                                  ImmutableHashSet.CreateRange(other.SentenceForms),
                                                  ImmutableHashSet.CreateRange(other.ConstantSentenceForms),
                                                  ImmutableHashSet.CreateRange(other.IndependentSentenceForms),
                                                  dependencyGraph,
                                                  rulesByForm,
                                                  trueSentencesByForm);
        }

        public ISentenceForm GetSentenceForm(Fact sentence)
        {
            return new SimpleSentenceForm(sentence);
        }

        public ISet<ISentenceForm> IndependentSentenceForms { get; private set; }

        public ISet<ISentenceForm> ConstantSentenceForms { get; private set; }

        public IDictionary<ISentenceForm, ICollection<ISentenceForm>> DependencyGraph { get { return _dependencyGraph.ToImmutableDictionary(); } }

        public ISet<Fact> GetSentencesListedAsTrue(ISentenceForm form)
        {
            return new HashSet<Fact>(_trueSentencesByForm[form]);
        }

        public ISet<Implication> GetRules(ISentenceForm form)
        {
            return new HashSet<Implication>(_rulesByForm[form]);
        }

        public ISet<ISentenceForm> SentenceForms { get; private set; }

        public IList<Expression> Description { get; private set; }
    }
}