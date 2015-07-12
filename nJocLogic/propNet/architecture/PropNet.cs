

/**
 * The PropNet class is designed to represent Propositional Networks.
 *
 * A propositional network (also known as a "propnet") is a way of representing
 * a game as a logic circuit. States of the game are represented by assignments
 * of TRUE or FALSE to "base" propositions, each of which represents a single
 * fact that can be true about the state of the game. For example, in a game of
 * Tic-Tac-Toe, the fact (cell 1 1 x) indicates that the cell (1,1) has an 'x'
 * in it. That fact would correspond to a base proposition, which would be set
 * to TRUE to indicate that the fact is true in the current state of the game.
 * Likewise, the base corresponding to the fact (cell 1 1 o) would be false,
 * because in that state of the game there isn't an 'o' in the cell (1,1).
 *
 * A state of the game is uniquely determined by the assignment of truth values
 * to the base propositions in the propositional network. Every assignment of
 * truth values to base propositions corresponds to exactly one unique state of
 * the game.
 *
 * Given the values of the base propositions, you can use the connections in
 * the network (AND gates, OR gates, NOT gates) to determine the truth values
 * of other propositions. For example, you can determine whether the terminal
 * proposition is true: if that proposition is true, the game is over when it
 * reaches this state. Otherwise, if it is false, the game isn't over. You can
 * also determine the value of the goal propositions, which represent facts
 * like (goal xplayer 100). If that proposition is true, then that fact is true
 * in this state of the game, which means that xplayer has 100 points.
 *
 * You can also use a propositional network to determine the next state of the
 * game, given the current state and the moves for each player. First, you set
 * the input propositions which correspond to each move to TRUE. Once that has
 * been done, you can determine the truth value of the transitions. Each base
 * proposition has a "transition" component going into it. This transition has
 * the truth value that its base will take on in the next state of the game.
 *
 * For further information about propositional networks, see:
 *
 * "Decomposition of Games for Efficient Reasoning" by Eric Schkufza.
 * "Factoring General Games using Propositional Automata" by Evan Cox et al.
 *
 * @author Sam Schreiber
 */

using System.Collections.Immutable;

namespace nJocLogic.propNet.architecture
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using data;
    using gameContainer;
    using knowledge;
    using NLog;

    public sealed class PropNet
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.propnet");

        public HashSet<IComponent> Components { get; private set; }							        /** References to every component in the PropNet. */
        public HashSet<IProposition> Propositions { get; private set; }						        /** References to every IProposition in the PropNet. */
        public Dictionary<Fact, IProposition> BasePropositions { get; private set; }			        /** References to every BaseProposition in the PropNet, indexed by name. */
        public Dictionary<Fact, IProposition> InputPropositions { get; private set; }				/** References to every InputProposition in the PropNet, indexed by name. */
        public Dictionary<TermObject, HashSet<IProposition>> LegalPropositions { get; private set; } /** References to every LegalProposition in the PropNet, indexed by role. */
        public Dictionary<TermObject, HashSet<IProposition>> GoalPropositions { get; private set; }	/** References to every GoalProposition in the PropNet, indexed by role. */
        public IProposition InitProposition { get; private set; }							        /** A reference to the single, unique, InitProposition. */
        public IProposition TerminalProposition { get; private set; }							    /** A reference to the single, unique, TerminalProposition. */
        public Dictionary<IProposition, IProposition> LegalInputMap { get; private set; }		        /** A helper mapping between input/legal propositions. */
        public List<TermObject> Roles { get; private set; }									        /** A helper list of all of the roles. */
        public HashSet<ITransition> Transitions { get; private set; }                               // Reference every transition in the PropNet 

        public void AddComponent(IComponent c)
        {
            Components.Add(c);
            var proposition = c as IProposition;
            if (proposition != null)
                Propositions.Add(proposition);
        }

        /// <summary>
        /// Creates a new PropNet from a list of Components, along with indices over
        /// those components.
        /// </summary>
        /// <param name="roles"></param>
        /// <param name="components">A list of Components.</param>
        public PropNet(List<TermObject> roles, HashSet<IComponent> components)
        {
            Roles = roles;
            Components = components;
            Propositions = RecordPropositions();
            BasePropositions = RecordBasePropositions();
            InputPropositions = RecordInputPropositions();
            LegalPropositions = RecordLegalPropositions();
            GoalPropositions = RecordGoalPropositions();
            InitProposition = RecordInitProposition();
            TerminalProposition = RecordTerminalProposition();
            LegalInputMap = MakeLegalInputMap();
            Transitions = new HashSet<ITransition>(Components.OfType<ITransition>());
        }

        private Dictionary<IProposition, IProposition> MakeLegalInputMap()
        {
            var doesProcessor = new RelationNameProcessor(GameContainer.Parser.TokDoes);
            var inputMap = new Dictionary<IProposition, IProposition>();

            var inputPropsByBody = new Dictionary<Fact, IProposition>();	// Create a mapping from Body->Input.
            foreach (IProposition inputProp in InputPropositions.Values)
                inputPropsByBody[inputProp.Name] = inputProp;

            // Use that mapping to map Input->Legal and Legal->Input
            // based on having the same Body proposition.
            foreach (HashSet<IProposition> legalProps in LegalPropositions.Values)
            {
                foreach (IProposition legalProp in legalProps)
                {
                    Fact legalPropBody = doesProcessor.ProcessBaseFact(legalProp.Name);
                    IProposition inputProp;
                    if (inputPropsByBody.TryGetValue(legalPropBody, out inputProp))
                    {
                        inputMap[inputProp] = legalProp;
                        inputMap[legalProp] = inputProp;
                    }
                }
            }
            return inputMap;
        }

        /// <summary>
        /// Returns a representation of the PropNet in .dot format.
        /// </summary>
        /// <returns></returns>
        public String ToDot()
        {
            var sb = new StringBuilder();

            sb.Append("digraph propNet\n{\n");
            foreach (IComponent component in Components)
                sb.Append("\t" + component.ToDot() + "\n");
            sb.Append("}");

            return sb.ToString();
        }

        public String ToDot(IComponent component)
        {
            var sb = new StringBuilder();

            sb.Append("digraph propNet\n{\n");
            sb.Append("\t" + component.ToDot() + "\n");

            Queue<IComponent> queue = new Queue<IComponent>();
            foreach (IComponent current in component.Inputs)
                queue.Enqueue(current);

            var done = new HashSet<IComponent>();

            while (queue.Count > 0)
            {
                IComponent current = queue.Dequeue();
                if (done.Contains(current))
                    continue;

                sb.Append("\t" + current.ToDot() + "\n");

                done.Add(current);

                if (current is ITransition)
                    continue;

                foreach (IComponent input in current.Inputs)
                    queue.Enqueue(input);
            }

            sb.Append("}");

            return sb.ToString();
        }


        /// <summary>
        /// Outputs the propnet in .dot format to a particular file.
        /// This can be viewed with tools like Graphviz and ZGRViewer.
        /// </summary>
        /// <param name="filename">the name of the file to output to</param>
        public void RenderToFile(String filename)
        {
            try
            {
                //var fout = new StreamWriter(filename, "UTF-8");
                using (var fout = new StreamWriter(filename))
                    fout.Write(ToDot());
            }
            catch (Exception e)
            {
                Logger.Error(e.StackTrace);
            }
        }

        public void RenderToFile(String filename, IComponent component)
        {
            try
            {
                using (var fout = new StreamWriter(filename))
                    fout.Write(ToDot(component));
            }
            catch (Exception e)
            {
                Logger.Error(e.StackTrace);
            }
        }

        /// <summary>
        /// Builds an index over the BasePropositions in the PropNet.
        /// 
        /// This is done by going over every single-input proposition in the network,
        /// and seeing whether or not its input is a transition, which would mean that
        /// by definition the proposition is a base proposition.</summary>
        /// <returns>An index over the BasePropositions in the PropNet.</returns>
        private Dictionary<Fact, IProposition> RecordBasePropositions()
        {
            var result = new Dictionary<Fact, IProposition>();
            foreach (var baseProp in Propositions.Where(p => p.IsBase))
                result[baseProp.Name] = baseProp;
            return result;
            //var baseProps = new Dictionary<Fact, IProposition>();
            //foreach (IProposition proposition in Propositions)
            //{
            //    // Skip all propositions without exactly one input.
            //    if (proposition.Inputs.Count != 1)
            //        continue;

            //    Component component = proposition.GetSingleInput();
            //    if (component is Transition)
            //        baseProps[proposition.Name] = proposition;
            //}

            //return baseProps;
        }

        /// <summary>
        /// Builds an index over the GoalPropositions in the PropNet.
        /// 
        /// This is done by going over every function proposition in the network
        /// where the name of the function is "goal", and extracting the name of the
        /// role associated with that goal proposition, and then using those role
        /// names as keys that map to the goal propositions in the index.
        /// </summary>
        /// <returns>An index over the GoalPropositions in the PropNet.</returns>
        private Dictionary<TermObject, HashSet<IProposition>> RecordGoalPropositions()
        {
            var goalProps = new Dictionary<TermObject, HashSet<IProposition>>();
            foreach (IProposition proposition in Propositions)
            {
                if (proposition.Name.RelationName == GameContainer.Parser.TokGoal)
                {
                    var theRole = (TermObject)proposition.Name.GetTerm(0); //TODO: could be a term variable instead?
                    if (!goalProps.ContainsKey(theRole))
                        goalProps[theRole] = new HashSet<IProposition>();
                    goalProps[theRole].Add(proposition);
                }
            }

            return goalProps;
        }

        /// <summary>
        /// Returns a reference to the single, unique, InitProposition.
        /// </summary>
        /// <returns>A reference to the single, unique, InitProposition.</returns>
        private IProposition RecordInitProposition()
        {
            //return Propositions.FirstOrDefault(proposition => proposition.Name.RelationName == GameContainer.Parser.TokInit);
            return Propositions.FirstOrDefault(proposition => proposition.Name.RelationName == GameContainer.SymbolTable["INIT"]);
        }

        /// <summary>
        /// Builds an index over the InputPropositions in the PropNet.
        /// </summary>
        /// <returns>An index over the InputPropositions in the PropNet.</returns>
        private Dictionary<Fact, IProposition> RecordInputPropositions()
        {
            var inputProps = new Dictionary<Fact, IProposition>();
            foreach (IProposition proposition in Propositions)
            {
                if (proposition.Name.RelationName == GameContainer.Parser.TokDoes)
                    inputProps[proposition.Name] = proposition;
            }

            return inputProps;
        }

        /// <summary>
        /// Builds an index over the LegalPropositions in the PropNet.
        /// </summary>
        /// <returns>An index over the LegalPropositions in the PropNet.</returns>
        private Dictionary<TermObject, HashSet<IProposition>> RecordLegalPropositions()
        {
            var legalProps = new Dictionary<TermObject, HashSet<IProposition>>();
            foreach (IProposition proposition in Propositions)
            {
                if (proposition.Name.RelationName == GameContainer.Parser.TokLegal)
                {
                    var role = (TermObject)proposition.Name.GetTerm(0);      //TODO: this could be a termvariable surely?
                    if (!legalProps.ContainsKey(role))
                        legalProps[role] = new HashSet<IProposition>();

                    legalProps[role].Add(proposition);
                }
            }

            return legalProps;
        }

        /// <summary>
        /// Builds an index over the Propositions in the PropNet.
        /// </summary>
        /// <returns>An index over Propositions in the PropNet.</returns>
        private HashSet<IProposition> RecordPropositions()
        {
            var props = new HashSet<IProposition>();
            foreach (var proposition in Components.OfType<IProposition>())
                props.Add(proposition);

            return props;
        }

        /// <summary>
        /// Records a reference to the single, unique, TerminalProposition.
        /// </summary>
        /// <returns>A reference to the single, unqiue, TerminalProposition.</returns>
        private IProposition RecordTerminalProposition()
        {
            return Propositions.FirstOrDefault(proposition => proposition.Name.RelationName == GameContainer.Parser.TokTerminal);
        }

        public int Size { get { return Components.Count; } }

        public int GetNumAnds()
        {
            return Components.OfType<IAnd>().Count();
        }

        public int GetNumOrs()
        {
            return Components.OfType<IOr>().Count();
        }

        public int GetNumNots()
        {
            return Components.OfType<INot>().Count();
        }

        public int GetNumLinks()
        {
            return Components.Sum(c => c.Outputs.Count);
        }

        /// <summary>
        /// Removes a component from the propnet. Be very careful when using
        /// this method, as it is not thread-safe. It is highly recommended
        /// that this method only be used in an optimization period between
        /// the propnet's creation and its initial use, during which it
        /// should only be accessed by a single thread.
        /// 
        /// The INIT and terminal components cannot be removed.
        /// </summary>
        /// <param name="c"></param>
        public void RemoveComponent(IComponent c)
        {
            //Go through all the collections it could appear in
            var proposition = c as IProposition;
            if (proposition != null)
            {
                var p = proposition;
                Fact name = p.Name;
                if (BasePropositions.ContainsKey(name))
                    BasePropositions.Remove(name);
                else if (InputPropositions.ContainsKey(name))
                {
                    InputPropositions.Remove(name);
                    //The map goes both ways...
                    IProposition partner = LegalInputMap[p];
                    if (partner != null)
                    {
                        LegalInputMap.Remove(partner);
                        LegalInputMap.Remove(p);
                    }
                }
                else if (name.RelationName == GameContainer.Parser.TokInit)
                    throw new Exception("The INIT component cannot be removed. Consider leaving it and ignoring it.");
                else if (name.RelationName == GameContainer.Parser.TokTerminal)
                    throw new Exception("The terminal component cannot be removed.");
                else
                {
                    foreach (HashSet<IProposition> props in LegalPropositions.Values.Where(props => props.Contains(p)))
                    {
                        props.Remove(p);
                        IProposition partner = LegalInputMap[p];
                        if (partner != null)
                        {
                            LegalInputMap.Remove(partner);
                            LegalInputMap.Remove(p);
                        }
                    }

                    foreach (HashSet<IProposition> props in GoalPropositions.Values)
                        props.Remove(p);
                }
                Propositions.Remove(p);
            }
            Components.Remove(c);

            //Remove all the local links to the component
            foreach (IComponent parent in c.Inputs)
                parent.RemoveOutput(c);
            foreach (IComponent child in c.Outputs)
                child.RemoveInput(c);
        }
    }
}