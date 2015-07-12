using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Wintellect.PowerCollections;
using System.Linq;

namespace nJocLogic.util.gdl.model
{
    /// <summary>
    /// When dealing with GDL, dependency graphs are often useful. DependencyGraphs offers a variety of functionality for dealing with dependency
    /// graphs expressed in the form of SetMultimaps.
    /// 
    /// These multimaps are paired with sets of all nodes, to account for the possibility of nodes not included in the multimap representation.
    /// 
    /// All methods assume that keys in multimaps depend on their associated values, or in other words are downstream of or are children of those 
    /// values.
    /// </summary>
    public class DependencyGraphs
    {
        private DependencyGraphs() { }

        /// <summary>
        /// Returns all elements of the dependency graph that match the given predicate, and any elements upstream of those matching elements.
        /// 
        /// The graph may contain cycles.
        /// 
        /// Each key in the dependency graph depends on/is downstream of its associated values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="allNodes"></param>
        /// <param name="dependencyGraph"></param>
        /// <param name="matcher"></param>
        /// <returns></returns>
        public static ImmutableHashSet<T> GetMatchingAndUpstream<T>(ISet<T> allNodes, MultiDictionary<T, T> dependencyGraph, Predicate<T> matcher)
        {
            ISet<T> results = new HashSet<T>();

            var toTry = new Deque<T>();
            toTry.AddManyToFront(allNodes.Where(a => matcher(a)));

            while (toTry.Any())
            {
                T curElem = toTry.RemoveFromFront();
                if (!results.Contains(curElem))
                {
                    results.Add(curElem);
                    toTry.AddManyToBack(dependencyGraph[curElem]);
                }
            }
            return results.ToImmutableHashSet();
        }

        /// <summary>
        /// Returns all elements of the dependency graph that match the given predicate, and any elements downstream of those matching
        /// elements.
        /// 
        /// The graph may contain cycles.
        /// 
        /// Each key in the dependency graph depends on/is downstream of its associated values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="allNodes"></param>
        /// <param name="dependencyGraph"></param>
        /// <param name="matcher"></param>
        /// <returns></returns>
        public static ImmutableHashSet<T> GetMatchingAndDownstream<T>(ISet<T> allNodes, MultiDictionary<T, T> dependencyGraph, Predicate<T> matcher)
        {
            return GetMatchingAndUpstream(allNodes, ReverseGraph(dependencyGraph), matcher);
        }

        //TODO: this implementation may not be the same as the java one - check
        public static MultiDictionary<T, T> ReverseGraph<T>(MultiDictionary<T, T> graph)
        {
            var result = new MultiDictionary<T, T>(false);
            foreach (KeyValuePair<T, ICollection<T>> kv in graph)
                foreach (T hkv in kv.Value)
                    result[hkv].Add(kv.Key);
            return result;
        }

        /// <summary>
        /// Given a dependency graph, return a topologically sorted ordering of its components, stratified in a way that allows for recursion and 
        /// cycles. (Each set in the list is one unordered "stratum" of elements. Elements may depend on elements in earlier strata or the same 
        /// stratum, but not in later strata.)
        /// 
        /// If there are no cycles, the result will be a list of singleton sets, topologically sorted.
        /// 
        /// Each key in the given dependency graph depends on/is downstream of its associated values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="allElements"></param>
        /// <param name="dependencyGraph"></param>
        /// <returns></returns>
        public static List<ISet<T>> ToposortSafe<T>(ISet<T> allElements, MultiDictionary<T, T> dependencyGraph)
        {
            ISet<ISet<T>> strataToAdd = CreateAllStrata(allElements);
            MultiDictionary<ISet<T>, ISet<T>> strataDependencyGraph = CreateStrataDependencyGraph(dependencyGraph);
            var ordering = new List<ISet<T>>();

            var iterator = strataToAdd.GetEnumerator();
            while (strataToAdd.Any())
            {
                iterator.MoveNext();
                ISet<T> curStratum = iterator.Current;
                AddOrMergeStratumAndAncestors(curStratum, ordering, strataToAdd, strataDependencyGraph, new List<ISet<T>>());
            }
            return ordering;
        }

        private static void AddOrMergeStratumAndAncestors<T>(ISet<T> curStratum,
                                                             ICollection<ISet<T>> ordering, ISet<ISet<T>> toAdd,
                                                             MultiDictionary<ISet<T>, ISet<T>> strataDependencyGraph,
                                                             IList<ISet<T>> downstreamStrata)
        {
            if (downstreamStrata.Contains(curStratum))
            {
                int mergeStartIndex = downstreamStrata.IndexOf(curStratum);
                var toMerge = new List<ISet<T>>();
                for (int i1 = mergeStartIndex; i1 < downstreamStrata.Count; i1++)
                    toMerge.Add(downstreamStrata[i1]);
                MergeStrata(new HashSet<ISet<T>>(toMerge), toAdd, strataDependencyGraph);
                return;
            }
            downstreamStrata.Add(curStratum);
            foreach (ISet<T> parent in strataDependencyGraph[curStratum].ToImmutableList())
            {
                //We could merge away the parent here, so we protect against CMEs and
                //make sure the parent is still in toAdd before recursing.
                if (toAdd.Contains(parent))
                    AddOrMergeStratumAndAncestors(parent, ordering, toAdd, strataDependencyGraph, downstreamStrata);
            }
            downstreamStrata.Remove(curStratum);
            // - If we've added all our parents, we will still be in toAdd
            //   and none of our dependencies will be in toAdd. Add to the ordering.
            // - If there was a merge upstream that we weren't involved in,
            //   we will still be in toAdd, but we will have (possibly new)
            //   dependencies that are still in toAdd. Do nothing.
            // - If there was a merge upstream that we were involved in,
            //   we won't be in toAdd anymore. Do nothing.
            if (!toAdd.Contains(curStratum))
                return;
            if (strataDependencyGraph[curStratum].Any(toAdd.Contains))
                return;
            ordering.Add(curStratum);
            toAdd.Remove(curStratum);
        }

        //Replace the old strata with the new stratum in toAdd and strataDependencyGraph.
        private static void MergeStrata<T>(ISet<ISet<T>> toMerge,
                                           ISet<ISet<T>> toAdd,
                                           MultiDictionary<ISet<T>, ISet<T>> strataDependencyGraph)
        {
            var merge = new HashSet<T>();
            foreach (var m in toMerge)
                merge.UnionWith(m);
            ISet<T> newStratum = merge.ToImmutableHashSet();
            foreach (ISet<T> oldStratum in toMerge)
                toAdd.Remove(oldStratum);
            toAdd.Add(newStratum);
            //Change the keys
            foreach (ISet<T> oldStratum in toMerge)
            {
                var parents = strataDependencyGraph[oldStratum];
                strataDependencyGraph.AddMany(newStratum, parents);
                strataDependencyGraph.Remove(oldStratum);
            }
            //Change the values
            foreach (KeyValuePair<ISet<T>, ICollection<ISet<T>>> mainEntry in strataDependencyGraph.ToImmutableList())
                foreach (ISet<T> entry in mainEntry.Value.Where(toMerge.Contains))
                {
                    strataDependencyGraph.Remove(mainEntry.Key, entry);
                    strataDependencyGraph.Add(mainEntry.Key, newStratum);
                }
        }

        private static ISet<ISet<T>> CreateAllStrata<T>(IEnumerable<T> allElements)
        {
            var result = new HashSet<ISet<T>>();
            foreach (T element in allElements)
                result.Add(ImmutableHashSet.Create(element));
            return result;
        }

        private static MultiDictionary<ISet<T>, ISet<T>> CreateStrataDependencyGraph<T>(IEnumerable<KeyValuePair<T, ICollection<T>>> dependencyGraph)
        {
            var strataDependencyGraph = new MultiDictionary<ISet<T>, ISet<T>>(false);
            foreach (KeyValuePair<T, ICollection<T>> entry in dependencyGraph)
                strataDependencyGraph.Add(ImmutableHashSet.Create(entry.Key), entry.Value.ToImmutableHashSet());
            return strataDependencyGraph;
        }
    }
}
