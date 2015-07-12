using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using nJocLogic.gdl;

namespace nJocLogic.knowledge
{
    public abstract class KnowledgeBase : IEnumerable<GroundFact>
    {
        /** Hash codes */
        protected object CleanHash;
        protected object ModHash;

        /// <summary>
        /// Setter for the hash object.
        /// </summary>
        /// <param name="hash">The object used for the hashing.</param>
        /// <param name="clean">True if we're setting the 'clean' hash.</param>
        public void SetHash(object hash, bool clean)
        {
            if (clean)
                CleanHash = hash;
            else
                ModHash = hash;
        }

        /// <summary>
        /// Getter for the hash object.
        /// </summary>
        /// <param name="clean">True if we're getting the 'clean' hash.</param>
        /// <returns>The object used for hashing the knowledge base.</returns>
        public object GetHash(bool clean)
        {
            return clean ? CleanHash : ModHash;
        }

        /**
         * Initialize it with a set of facts
         * @param symtab
         *            The symbol table to use for the facts in this knowledge base.  
         * @param facts
         *            The facts to store in the KB
         */
        public void LoadWithFacts(List<GroundFact> facts)
        {
            foreach (GroundFact fact in facts)
                SetTrue(fact);
        }

        /**
         * Clear all relations such that nothing is true anymore.
         */
        public abstract void Clear();

        /**
         * Get the number of facts true in the current state.
         * 
         * @return The number of true facts.
         */
        public abstract int GetNumFacts();

        public abstract bool IsTrue(GroundFact fact);

        public bool IsTrue(Fact fact)
        {
            // If it's not a ground fact, it's false
            if (fact is GroundFact == false)
                return false;

            return IsTrue((GroundFact)fact);
        }

        /**
         * Mark a given fact as 'true' in the current state.
         * 
         * @param fact Fact to set as true.
         */
        public abstract void SetTrue(GroundFact fact);

        /**
         * Mark a given fact as 'false' in the current state.
         * Note that given 'negation as failure' this means the same
         * thing as removing a fact from the database.
         * 
         * @param fact Fact to set as false.
         */
        public abstract void SetFalse(GroundFact fact);

        /**
         * Get the GDL state as a string. Note that in many cases using
         * the Writer version of this method will be more efficient, since the string
         * memory doesn't have to be allocated twice.
         * 
         * @return A string containing the state of this knowledge base in GDL sentences.
         */
        public string StateToGdl()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    StateToGdl(writer);
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);
                    return (new StreamReader(stream)).ReadToEnd();
                }
            }
        }

        /**
         * Write the state in GDL to a Writer (character stream). Uses the Game Manager's
         * symbol table.
         * 
         * @param target The Writer to output to.
         */
        public void StateToGdl(StreamWriter target)
        {
            StateToGdl(target, GameContainer.SymbolTable);
        }

        public abstract void StateToGdl(StreamWriter target, SymbolTable symTab);

        /**
         * Return a sorted list of facts true in this database. The ordering is
         * according to the elements' Comparable interface.
         * 
         * @return A list of all facts in this database.
         */
        public List<GroundFact> GetFacts()
        {
            return GetFacts(true);
        }


        /**
         * Get the number of differences with another kb. A 'difference' is defined
         * as a fact in one KB but not the other.
         * 
         * <p>
         * Formally: # differences = #(this - other) + #(other - this)
         * 
         * @param otherKb
         *            The KB to compare differences with.
         * @return The number of differences between the two kb.
         */
        public int GetDifferences(KnowledgeBase otherKb)
        {
            return this.Count(otherKb.IsTrue) + otherKb.Count(IsTrue);
        }

        /**
         * Return a list of all facts true in this database.
         * 
         * @param sorted True if the list should be sorted.
         * @return A list of all facts true in this database.
         */
        public abstract List<GroundFact> GetFacts(bool sorted);

        /**
         * Retrieves all the facts in the kb that are unifiable with the input (variable) fact.
         * @param fact      The template fact to match kb facts against
         * @return          A list of fact that are unifiable.
         */        
        public abstract List<Substitution> GetUnifiable(VariableFact fact);

        public abstract IEnumerator<GroundFact> GetEnumerator();

        public abstract bool ContainsRelationName(Fact fact);

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return GetFacts().Aggregate(string.Empty, (current, f) => current + (f.ToString() + "\n"));
        }
    }
}
