using System;
using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.reasoner.AimaProver
{
    public class ProverCache
    {
        private readonly Dictionary<Fact, ICollection<Fact>> _contents;

        public ProverCache()
        {
            _contents = new Dictionary<Fact, ICollection<Fact>>();
        }

        public bool Contains(Fact sentence)
        {
            return _contents.ContainsKey(new VariableRenamer().Rename(sentence));
        }

        public ICollection<Substitution> this[Fact sentence]
        {
            get
            {
                var results = new HashSet<Substitution>();
                ICollection<Fact> facts;
                if ( _contents.TryGetValue(new VariableRenamer().Rename(sentence), out facts))
                    foreach (Fact answer in facts)
                        results.Add(Unifier.Unify(sentence, answer));

                return new List<Substitution>(results);
            }
            set
            {
                var results = new HashSet<Fact>();
                foreach (Substitution answer in value)
                    results.Add(Substituter.Substitute(sentence, answer));

                _contents[new VariableRenamer().Rename(sentence)] = results;
            }
        }
    }

}