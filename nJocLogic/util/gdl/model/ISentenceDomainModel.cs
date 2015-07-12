namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// An extension of the SentenceFormModel that additionally
    /// includes information about the domains of sentence forms.
    /// In other words, this model specifies which constants can
    /// be in which positions of each sentence form.
    /// 
    /// The recommended way to create a SentenceDomainModel is
    /// via {@link SentenceDomainModelFactory#createWithCartesianDomains(java.util.List)}.
    /// </summary>
    public interface ISentenceDomainModel : ISentenceFormModel, ISentenceDomain { }

    public interface ISentenceDomain
    {
        /// <summary>
        /// Gets the domain of a particular sentence form, which has
        /// information about which particular sentences of the given
        /// sentence form are possible.
        /// </summary>
        ISentenceFormDomain GetDomain(ISentenceForm form);
    }
}
