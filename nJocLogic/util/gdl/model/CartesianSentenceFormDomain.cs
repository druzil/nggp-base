using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// A <see cref="ISentenceFormDomain"/> implementation that stores which constant values are possible for each slot of a sentence form.
    /// 
    /// This is a more compact representation than a {@link FullSentenceFormDomain}, but has less expressive power.
    /// </summary>
    public class CartesianSentenceFormDomain : ISentenceFormDomain
    {
        private readonly ISentenceForm _form;
        private readonly ImmutableList<JavaHashSet<TermObject>> _domainsForSlots;

        public CartesianSentenceFormDomain(ISentenceForm form, IEnumerable<ISet<TermObject>> domainsForSlots)
        {
            _form = form;
            _domainsForSlots = domainsForSlots.Select(s => new JavaHashSet<TermObject>(s)).ToImmutableList();
        }

        public CartesianSentenceFormDomain(ISentenceForm form, IDictionary<int, ICollection<TermObject>> setMultimap)
        {
            Debug.Assert(setMultimap != null);

            var domainsForSlots = new List<ISet<TermObject>>();
            for (int i = 0; i < form.TupleSize; i++)
            {
                ICollection<TermObject> possibleDomain;
                if (!setMultimap.TryGetValue(i, out possibleDomain))
                    possibleDomain = new List<TermObject>();
                domainsForSlots.Add(new HashSet<TermObject>(possibleDomain));
            }
            _form = form;
            _domainsForSlots = domainsForSlots.Select(s => new JavaHashSet<TermObject>(s)).ToImmutableList();
        }

        public ISentenceForm Form { get { return _form; } }

        ISet<TermObject> ISentenceFormDomain.GetDomainForSlot(int slotIndex)
        {
            Debug.Assert(slotIndex < _form.TupleSize);
            return _domainsForSlots[slotIndex];
        }

        public IEnumerator<Fact> GetEnumerator()
        {
            //return _domainsForSlots.Select(d => _form.GetSentenceFromTuple(d.Select(d1 => (Term)d1).ToList())).GetEnumerator();
            IEnumerable<IEnumerable<Term>> cartesianProject = _domainsForSlots.CartesianProduct<Term>();
            return cartesianProject.Select(c => _form.GetSentenceFromTuple(c.ToList())).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (JavaHashSet<TermObject> slot in _domainsForSlots)
                sb.Append(string.Format("({0}) ", string.Join(",", slot)));
            return sb.ToString();
        }
    }
}
