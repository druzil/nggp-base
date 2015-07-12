using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using nJocLogic.propNet.architecture;
using Wintellect.PowerCollections;

namespace nJocLogic.propNet.factory
{
    class PropNetFactoryUtils
    {
        private readonly IComponentFactory _componentFactory;

        public PropNetFactoryUtils(IComponentFactory componentFactory)
        {
            _componentFactory = componentFactory;
        }

        /// <summary>
        /// Represents the "type" of a node with respect to which truth values it is capable of having: 
        /// true, false, either value, or neither value. Used by
        /// <see cref="PropNetFactoryUtils.RemoveUnreachableBasesAndInputs"/>
        /// </summary>
        private enum TypeCode
        {
            Neither, //(false, false),
            True, //(true, false),
            False, //(false, true),
            Both //(true, true);
        }

        private class Type
        {
            internal readonly bool HasTrue;
            internal readonly bool HasFalse;

            public Type(TypeCode code)
            {
                HasTrue = code == TypeCode.Both || code == TypeCode.True;
                HasFalse = code == TypeCode.Both || code == TypeCode.False;
            }

            public static implicit operator TypeCode(Type type)
            {
                if (type.HasTrue)
                    return type.HasFalse ? TypeCode.Both : TypeCode.True;
                return type.HasFalse ? TypeCode.False : TypeCode.Neither;
            }

            public bool Includes(TypeCode other)
            {
                switch (other)
                {
                    case TypeCode.Both:
                        return HasTrue && HasFalse;
                    case TypeCode.False:
                        return HasFalse;
                    case TypeCode.Neither:
                        return true;
                    case TypeCode.True:
                        return HasTrue;
                }
                throw new Exception();
            }

            public TypeCode With(TypeCode otherType)
            {
                switch (otherType)
                {
                    case TypeCode.Both:
                        return TypeCode.Both;
                    case TypeCode.Neither:
                        return this;
                    case TypeCode.True:
                        return HasFalse ? TypeCode.Both : TypeCode.True;
                    case TypeCode.False:
                        return HasTrue ? TypeCode.Both : TypeCode.False;
                }
                throw new Exception();
            }

            public TypeCode Minus(TypeCode other)
            {
                switch (other)
                {
                    case TypeCode.Both:
                        return TypeCode.Neither;
                    case TypeCode.True:
                        return HasFalse ? TypeCode.False : TypeCode.Neither;
                    case TypeCode.False:
                        return HasTrue ? TypeCode.True : TypeCode.Neither;
                    case TypeCode.Neither:
                        return this;
                }
                throw new Exception();
            }

            public TypeCode Opposite()
            {
                switch ((TypeCode)this)
                {
                    case TypeCode.True:
                        return TypeCode.False;
                    case TypeCode.False:
                        return TypeCode.True;
                    case TypeCode.Neither:
                    case TypeCode.Both:
                        return this;
                }
                throw new Exception();
            }
        }

        /// <summary>
        /// Removes from the propnet all components that are discovered through type
        /// inference to only ever be true or false, replacing them with their values
        /// appropriately. This method may remove base and input propositions that are
        /// shown to be always false (or, in the case of base propositions, those that
        /// are always true).
        /// </summary>
        /// <param name="pn"></param>
        /// <param name="basesTrueByInit">The set of base propositions that are true on the first turn of the game.</param>
        public void RemoveUnreachableBasesAndInputs(PropNet pn, HashSet<IProposition> basesTrueByInit)
        {
            //If this doesn't contain a component, that's the equivalent of Type.NEITHER
            var reachability = new Dictionary<IComponent, Type>();
            //Keep track of the number of true inputs to AND gates and false inputs to
            //OR gates.
            var numTrueInputs = new Bag<IComponent>();
            var numFalseInputs = new Bag<IComponent>();
            var toAdd = new Stack<Tuple<IComponent, TypeCode>>();

            //It's easier here if we get just the one-way version of the map
            var legalsToInputs = new Dictionary<IProposition, IProposition>();
            foreach (IProposition legalProp in pn.LegalPropositions.Values.SelectMany(v => v))
            {
                IProposition inputProp = pn.LegalInputMap[legalProp];
                if (inputProp != null)
                    legalsToInputs[legalProp] = inputProp;
            }

            //All constants have their values
            foreach (IComponent c in pn.Components) //ConcurrencyUtils.checkForInterruption();
                if (c is IConstant)
                    toAdd.Push(c.Value ? Tuple.Create(c, TypeCode.True) : Tuple.Create(c, TypeCode.False));

            //Every input can be false (we assume that no player will have just one move allowed all game)
            foreach (IProposition p in pn.InputPropositions.Values)
                toAdd.Push(Tuple.Create((IComponent)p, TypeCode.False));

            //Every base with "init" can be true, every base without "init" can be false
            foreach (IProposition baseProp in pn.BasePropositions.Values)
                toAdd.Push(basesTrueByInit.Contains(baseProp)
                    ? Tuple.Create((IComponent)baseProp, TypeCode.True)
                    : Tuple.Create((IComponent)baseProp, TypeCode.False));

            //Keep INIT, for those who use it
            IProposition initProposition = pn.InitProposition;
            toAdd.Push(Tuple.Create((IComponent)initProposition, TypeCode.Both));

            while (toAdd.Any())
            {
                //ConcurrencyUtils.checkForInterruption();
                Tuple<IComponent, TypeCode> curEntry = toAdd.Pop();
                IComponent curComp = curEntry.Item1;
                var newInputType = new Type(curEntry.Item2);
                Type oldType = reachability[curComp] ?? new Type(TypeCode.Neither);

                //We want to send only the new addition to our children,
                //for consistency in our parent-true and parent-false
                //counts.
                //Make sure we don't double-apply a type.

                var typeToAdd = new Type(TypeCode.Neither); // Any new values that we discover we can have this iteration.
                if (curComp is IProposition)
                    typeToAdd = newInputType;
                else if (curComp is ITransition)
                    typeToAdd = newInputType;
                else if (curComp is IConstant)
                    typeToAdd = newInputType;
                else if (curComp is INot)
                    typeToAdd = new Type(newInputType.Opposite());
                else if (curComp is IAnd)
                {
                    if (newInputType.HasTrue)
                    {
                        numTrueInputs.Add(curComp);
                        if (numTrueInputs.Count(n => n == curComp) == curComp.Inputs.Count)
                            typeToAdd = new Type(TypeCode.True);
                    }
                    if (newInputType.HasFalse)
                        typeToAdd = new Type(typeToAdd.With(TypeCode.False));
                }
                else if (curComp is IOr)
                {
                    if (newInputType.HasFalse)
                    {
                        numFalseInputs.Add(curComp);
                        if (numFalseInputs.Count(n => n == curComp) == curComp.Inputs.Count)
                            typeToAdd = new Type(TypeCode.False);
                    }
                    if (newInputType.HasTrue)
                        typeToAdd = new Type(typeToAdd.With(TypeCode.True));
                }
                else
                    throw new Exception("Unhandled component type " + curComp.GetType());

                if (oldType.Includes(typeToAdd)) //We don't know anything new about curComp
                    continue;

                reachability[curComp] = new Type(typeToAdd.With(oldType));
                typeToAdd = new Type(typeToAdd.Minus(oldType));
                if (typeToAdd == TypeCode.Neither)
                    throw new Exception("Something's messed up here");

                //Add all our children to the stack
                foreach (IComponent output in curComp.Outputs)
                    toAdd.Push(Tuple.Create(output, (TypeCode)typeToAdd));

                if (legalsToInputs.ContainsKey((IProposition)curComp))
                {
                    IProposition inputProp = legalsToInputs[(IProposition)curComp];
                    if (inputProp == null)
                        throw new Exception("IllegalState");

                    toAdd.Push(Tuple.Create((IComponent)inputProp, (TypeCode)typeToAdd));
                }
            }

            IConstant trueConst = _componentFactory.CreateConstant(true);
            IConstant falseConst = _componentFactory.CreateConstant(false);
            pn.AddComponent(trueConst);
            pn.AddComponent(falseConst);
            //Make them the input of all false/true components
            foreach (var entry in reachability)
            {
                TypeCode type = entry.Value;
                if (type == TypeCode.True || type == TypeCode.False)
                {
                    IComponent c = entry.Key;
                    if (c is IConstant) //Don't bother trying to remove this
                        continue;

                    //Disconnect from inputs
                    foreach (IComponent input in c.Inputs)
                        input.RemoveOutput(c);

                    c.RemoveAllInputs();
                    if (type == TypeCode.True ^ (c is INot))
                    {
                        c.AddInput(trueConst);
                        trueConst.AddOutput(c);
                    }
                    else
                    {
                        c.AddInput(falseConst);
                        falseConst.AddOutput(c);
                    }
                }
            }

            OptimizingPropNetFactory.OptimizeAwayTrueAndFalse(pn, trueConst, falseConst);
        }

        /// <summary>
        /// Optimizes an already-existing propnet by removing useless leaves. These are components that have no 
        /// outputs, but have no special meaning in GDL that requires them to stay.
        /// </summary>
        /// <param name="pn"></param>
        public static void LopUselessLeaves(PropNet pn)
        {
            //Approach: Collect useful propositions based on a backwards
            //search from goal/legal/terminal (passing through transitions)
            var usefulComponents = new HashSet<IComponent>();
            //TODO: Also try with queue?
            var toAdd = new Stack<IComponent>();
            toAdd.Push(pn.TerminalProposition);
            usefulComponents.Add(pn.InitProposition); //Can't remove it...
            IEnumerable<IProposition> goalProps = pn.GoalPropositions.Values.SelectMany(goalProp => goalProp);
            foreach (IProposition prop in goalProps)
                toAdd.Push(prop);

            IEnumerable<IProposition> legalProps = pn.LegalPropositions.Values.SelectMany(legalProp => legalProp);
            foreach (IProposition prop in legalProps)
                toAdd.Push(prop);

            while (toAdd.Any())
            {
                IComponent curComp = toAdd.Pop();
                if (usefulComponents.Contains(curComp))
                    //We've already added it
                    continue;
                usefulComponents.Add(curComp);
                foreach (IComponent input in curComp.Inputs)
                    toAdd.Push(input);
            }

            //Remove the components not marked as useful
            var allComponents = new List<IComponent>(pn.Components);
            foreach (IComponent c in allComponents)
                if (!usefulComponents.Contains(c))
                    pn.RemoveComponent(c);
        }

        /// <summary>
        /// Optimizes an already-existing propnet by removing propositions of the form (init ?x). Does NOT remove the proposition "INIT".
        /// </summary>
        /// <param name="pn"></param>
        public static void RemoveInits(PropNet pn)
        {
            var toRemove = (from p in pn.Propositions
                            let relation = p.Name
                            where relation.RelationName == GameContainer.Parser.TokInit
                            select p).ToList();

            foreach (IProposition p in toRemove)
                pn.RemoveComponent(p);
        }

        /// <summary>
        /// Potentially optimizes an already-existing propnet by removing propositions with no special meaning. The inputs and outputs 
        /// of those propositions are connected to one another. This is unlikely to improve performance unless values of every single 
        /// component are stored (outside the propnet).
        /// </summary>
        /// <param name="pn"></param>
        public static void RemoveAnonymousPropositions(PropNet pn)
        {
            var toSplice = new List<IProposition>();
            var toReplaceWithFalse = new List<IProposition>();
            foreach (IProposition p in pn.Propositions)
            {
                //If it's important, continue to the next proposition
                if (p.Inputs.Count == 1 && p.GetSingleInput() is ITransition)
                    //It's a base proposition
                    continue;
                Fact sentence = p.Name;

                Fact relation = sentence;
                int name = relation.RelationName;
                var parser = GameContainer.Parser;
                if (name == parser.TokLegal || name == parser.TokGoal || name == parser.TokDoes || name == parser.TokInit ||
                    name == parser.TokTerminal
                    || sentence.RelationName == GameContainer.SymbolTable["INIT"])
                    continue;

                if (p.Inputs.Count < 1)
                {
                    //Needs to be handled separately...
                    //because this is an always-false true proposition
                    //and it might have and gates as outputs
                    toReplaceWithFalse.Add(p);
                    continue;
                }
                if (p.Inputs.Count != 1)
                    throw new Exception(string.Format("Might have falsely declared {0} to be unimportant?", p.Name));
                //Not important
                //System.out.println("Removing " + p);
                toSplice.Add(p);
            }

            foreach (IProposition p in toSplice)
            {
                //Get the inputs and outputs...
                HashSet<IComponent> inputs = p.Inputs;
                HashSet<IComponent> outputs = p.Outputs;
                //Remove the proposition...
                pn.RemoveComponent(p);
                //And splice the inputs and outputs back together
                if (inputs.Count > 1)
                    throw new Exception("Programmer made a bad assumption here... might lead to trouble?");
                foreach (IComponent input in inputs)
                    foreach (IComponent output in outputs)
                    {
                        input.AddOutput(output);
                        output.AddInput(input);
                    }
            }
            foreach (IProposition p in toReplaceWithFalse)
                throw new Exception(string.Format("Should be replacing {0} with false, but should do that in the OPNF, really; better equipped to do that there", p));
        }

    }
}
