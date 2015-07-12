using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// Allows SentenceDomainModels to delegate their ISentenceFormModel aspects to an existing ISentenceFormModel.
    /// </summary>
    public abstract class AbstractSentenceDomainModel : ISentenceDomainModel
    {
        private readonly ISentenceFormModel _formModel;

        public abstract ISentenceFormDomain GetDomain(ISentenceForm form);

        protected AbstractSentenceDomainModel(ISentenceFormModel formModel)
        {
            _formModel = formModel;
        }

        internal ISentenceFormModel FormModel { get { return _formModel; } }

        public ISet<ISentenceForm> IndependentSentenceForms { get { return _formModel.IndependentSentenceForms; } }

        public ISet<ISentenceForm> ConstantSentenceForms { get { return _formModel.ConstantSentenceForms; } }

        public IDictionary<ISentenceForm, ICollection<ISentenceForm>> DependencyGraph { get { return _formModel.DependencyGraph; } }

        public ISet<ISentenceForm> SentenceForms { get { return _formModel.SentenceForms; } }

        public IList<Expression> Description { get { return _formModel.Description; } }

        public ISet<Fact> GetSentencesListedAsTrue(ISentenceForm form)
        {
            return _formModel.GetSentencesListedAsTrue(form);
        }

        public ISet<Implication> GetRules(ISentenceForm form)
        {
            return _formModel.GetRules(form);
        }

        public ISentenceForm GetSentenceForm(Fact sentence)
        {
            return _formModel.GetSentenceForm(sentence);
        }
    }
}
