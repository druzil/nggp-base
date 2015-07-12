using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    public class ImmutableSentenceDomainModel : AbstractSentenceDomainModel
    {
        private readonly ImmutableDictionary<ISentenceForm, ISentenceFormDomain> _domains;

        public ImmutableSentenceDomainModel(ISentenceFormModel formModel, IDictionary<ISentenceForm, ISentenceFormDomain> domains)
            : base(ImmutableSentenceFormModel.CopyOf(formModel))
        {
            //if (!formModel.SentenceForms.SetEquals(domains.Keys))
            //    throw new Exception();
            _domains = domains.ToImmutableDictionary();
        }

        public static ImmutableSentenceDomainModel CopyUsingCartesianDomains(ISentenceDomainModel otherModel)
        {
            var immutableSentenceDomainModel = otherModel as ImmutableSentenceDomainModel;
            if (immutableSentenceDomainModel != null)
                return immutableSentenceDomainModel;

            var domains = ImmutableDictionary.CreateBuilder<ISentenceForm, ISentenceFormDomain>();
            foreach (ISentenceForm form in otherModel.SentenceForms)
            {
                ISentenceFormDomain otherDomain = otherModel.GetDomain(form);
                var domainsForSlots = new List<ISet<TermObject>>();
                for (int i = 0; i < form.TupleSize; i++)
                    domainsForSlots.Add(otherDomain.GetDomainForSlot(i));
                domains[form] = new CartesianSentenceFormDomain(form, domainsForSlots);
            }
            return new ImmutableSentenceDomainModel(ImmutableSentenceFormModel.CopyOf(otherModel), domains.ToImmutable());
        }

        public override ISentenceFormDomain GetDomain(ISentenceForm form)
        {
            ISentenceFormDomain result;
            _domains.TryGetValue(form, out result);
            return result;
        }
    }
}
