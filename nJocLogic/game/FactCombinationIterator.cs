using System.Collections;
using System.Collections.Generic;
using nJocLogic.data;
using nJocLogic.knowledge;

namespace nJocLogic.game
{
    public class FactCombinationIterator : IEnumerable<GroundFact[]>
    {
        private bool _endOfIteration;
        private readonly int[] _currentIndeces;
        private readonly GroundFact[] _currentFacts;
        private readonly List<List<GroundFact>> _items;
        private readonly FactProcessor _processor;

        private int _changeLevels;

        class DummyFactProcessor : FactProcessor
        {
            public override GroundFact ProcessFact(GroundFact fact)
            {
                return fact;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="staticFact">The fact that will appear in all results.</param>
        /// <param name="items">The facts from which to generate combinations.</param>        
        public FactCombinationIterator(GroundFact staticFact, List<List<GroundFact>> items)
            : this(staticFact, items, new DummyFactProcessor()) { }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="staticFact">The fact that will appear in all results.</param>
        /// <param name="items">The facts from which to generate combinations.</param>
        /// <param name="processor">The processor to apply to all facts.</param>
        public FactCombinationIterator(GroundFact staticFact, List<List<GroundFact>> items, FactProcessor processor)
        {
            _processor = processor;
            _items = items;
            _currentIndeces = new int[_items.Count + 1];
            _currentFacts = new GroundFact[_items.Count + 1];
            _currentFacts[0] = processor.ProcessFact(staticFact);
            _changeLevels = _items.Count;
        }

        private void PrepareNextIndeces()
        {
            _changeLevels = 0;
            IncrementIndex(_currentIndeces.Length - 1);
        }

        private void IncrementIndex(int index)
        {
            if (index <= 0)
            {
                _endOfIteration = true;
                return;
            }
            _changeLevels++;
            int newIndexValue = _currentIndeces[index] + 1;
            if (newIndexValue >= _items[index - 1].Count)
            {
                _currentIndeces[index] = 0;
                IncrementIndex(index - 1);
            }
            else
                _currentIndeces[index] = newIndexValue;
        }

        private void PrepareFacts()
        {
            for (int i = 0; i < _changeLevels; i++)
            {
                int index = _currentFacts.Length - 1 - i;
                _currentFacts[index] = _processor.ProcessFact(_items[index - 1][_currentIndeces[index]]);
            }
        }

        public IEnumerator<GroundFact[]> GetEnumerator()
        {
            while (!_endOfIteration)
            {
                PrepareFacts();
                PrepareNextIndeces();
                yield return _currentFacts;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }
    }

}
