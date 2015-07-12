using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using nJocLogic.data;
using nJocLogic.util.gdl.model;
using Wintellect.PowerCollections;

namespace nJocLogic.util.gdl.transforms
{
    public class ImmutableConstantChecker : IConstantChecker
    {
        private readonly ImmutableSentenceFormModel _sentenceModel;
        private readonly MultiDictionary<ISentenceForm, Fact> _sentencesByForm;	//TODO: Immutable
        private readonly ImmutableHashSet<Fact> _allSentences;

        private ImmutableConstantChecker(ImmutableSentenceFormModel sentenceModel, MultiDictionary<ISentenceForm, Fact> sentencesByForm)
        {
            Debug.Assert(sentenceModel.ConstantSentenceForms.IsSupersetOf(sentencesByForm.Keys));
            _sentenceModel = sentenceModel;
            _sentencesByForm = sentencesByForm;
            _allSentences = sentencesByForm.SelectMany(s => s.Value).ToImmutableHashSet();
        }

        /// <summary>
        /// Returns an ImmutableConstantChecker with contents identical to the given ConstantChecker.
        /// May not actually make a copy if the input is immutable.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public static ImmutableConstantChecker CopyOf(IConstantChecker other)
        {
            var immutableConstantChecker = other as ImmutableConstantChecker;
            if (immutableConstantChecker != null)
                return immutableConstantChecker;

            ISentenceFormModel model = other.SentenceFormModel;
            var sentencesByForm = new MultiDictionary<ISentenceForm, Fact>(false);
            foreach (ISentenceForm form in other.ConstantSentenceForms)
                sentencesByForm[form] = other.GetTrueSentences(form);

            return new ImmutableConstantChecker(ImmutableSentenceFormModel.CopyOf(model), sentencesByForm);
        }

        public static ImmutableConstantChecker Create(ISentenceFormModel sentenceModel, MultiDictionary<ISentenceForm, Fact> sentencesByForm)
        {
            return new ImmutableConstantChecker(ImmutableSentenceFormModel.CopyOf(sentenceModel), sentencesByForm);
        }

        public bool HasConstantForm(Fact sentence)
        {
            return ConstantSentenceForms.Any(form => form.Matches(sentence));
        }

        public bool IsConstantForm(ISentenceForm form)
        {
            return _sentenceModel.ConstantSentenceForms.Contains(form);
        }

        public ISet<Fact> GetTrueSentences(ISentenceForm form)
        {
            return new HashSet<Fact>(_sentencesByForm[form]);
        }

        public ISet<ISentenceForm> ConstantSentenceForms { get { return _sentenceModel.ConstantSentenceForms; } }

        public bool IsTrueConstant(Fact sentence)
        {
            ////TODO: This could be even more efficient; we don't need to bucket by form
            //ISentenceForm form = _sentenceModel.GetSentenceForm(sentence);
            //return _sentencesByForm[form].Contains(sentence);
            return _allSentences.Contains(sentence);
        }

        public ISentenceFormModel SentenceFormModel { get { return _sentenceModel; } }
    }
}
