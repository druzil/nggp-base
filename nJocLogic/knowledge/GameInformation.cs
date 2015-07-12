using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.knowledge
{
    public class GameInformation
    {
        private C5.TreeDictionary<int, RelationInfo> _relations;

        /** Rules used by the game. Indexed by rule head. */
        private C5.TreeDictionary<int, List<Implication>> _rules;

        /**
         * Ground facts that were extracted during GDL examination. (This includes
         * static and init facts.)
         */
        private C5.TreeDictionary<int, List<GroundFact>> _groundFacts;

        private List<TermObject> _roles;

        public List<GroundFact> GetAllGrounds()
        {
            var grounds = new List<GroundFact>();

            foreach (RelationInfo r in _relations.Values)
                grounds.AddRange(_groundFacts[r.Name]);

            return grounds;
        }

        /**
         * @param groundFacts The game's ground facts.
         */
        public void SetGroundFacts(C5.TreeDictionary<int, List<GroundFact>> groundFacts)
        {
            _groundFacts = groundFacts;
        }

        /**
         * @param relations The relations to set.
         */
        public void SetRelations(C5.TreeDictionary<int, RelationInfo> relations)
        {
            _relations = relations;
        }

        /**
         * @return Returns the roles.
         */
        public List<TermObject> GetRoles()
        {
            return _roles;
        }

        /**
         * @param roles The roles to set.
         */
        public void SetRoles(List<TermObject> roles)
        {
            _roles = roles;
        }

        /**
         * @return Returns the game's rules, indexed by rule head relation.
         */
        public C5.TreeDictionary<int, List<Implication>> GetIndexedRules()
        {
            return _rules;
        }

        public List<Implication> GetRules()
        {
            var allRules = new List<Implication>();

            foreach (List<Implication> r in _rules.Values)
                allRules.AddRange(r);

            return allRules;
        }

        /**
         * @param rules The rules used by the game.
         */
        public void SetRules(C5.TreeDictionary<int, List<Implication>> rules)
        {
            _rules = rules;
        }
    }
}
