using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    public class SentenceDomainModelFactory
    {
        /// <summary>
        /// Find all sentence forms (e.g. (legal _ _)) <para />
        /// along with the possible constants that can be bound to each column of the form
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        public static ImmutableSentenceDomainModel CreateWithCartesianDomains(IList<Expression> description)
        {
            ImmutableSentenceFormModel formModel = SentenceFormModelFactory.Create(description);

            var sentenceFormsFinder = new SentenceFormsFinder(formModel.Description);
            Dictionary<ISentenceForm, ISentenceFormDomain> domains = sentenceFormsFinder.FindCartesianDomains();

            return new ImmutableSentenceDomainModel(formModel, domains);
        }
    }
}
