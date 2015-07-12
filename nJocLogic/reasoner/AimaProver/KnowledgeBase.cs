using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.reasoner.AimaProver
{
    public class KnowledgeBase
    {
        private readonly Dictionary<int, List<Implication>> _contents;

        public KnowledgeBase(IEnumerable<Expression> description)
        {
            _contents = new Dictionary<int, List<Implication>>();
            foreach (Expression gdl in description)
            {
                var implication = gdl as Implication;
                var rule = implication ?? new Implication((Fact) gdl);
                int key = rule.Consequent.RelationName;

                if (!_contents.ContainsKey(key))
                    _contents[key] = new List<Implication>();
                _contents[key].Add(rule);
            }
        }

        public List<Implication> Fetch(Fact sentence)
        {
            return _contents.ContainsKey(sentence.RelationName) 
                ? _contents[sentence.RelationName] 
                : new List<Implication>();
        }
    }
}