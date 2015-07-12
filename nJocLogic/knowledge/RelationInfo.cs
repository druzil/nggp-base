using System;
using nJocLogic.gameContainer;

namespace nJocLogic.knowledge
{
public class RelationInfo : IComparable<RelationInfo>
{
    readonly private int _name;
    readonly private int _arity;
    
    public RelationInfo(int name, int arity)
    {
        _name = name;
        _arity = arity;
    }
    
    /**
     * @return Returns the arity of this relation.
     */
    public int Arity
    {
        get { return _arity; }
    }

    /**
     * @return Returns the name of this relation.
     */
    public int Name
    {
        get { return _name;}
    }

    public int CompareTo( RelationInfo o )
    {
        return Math.Sign(_name - o._name);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public override bool Equals( object obj )
    {
        if ( this == obj )
            return true;
        if ( obj is RelationInfo == false )
            return false;
        
        return _name == ((RelationInfo) obj)._name;
    }

    public override string ToString()
    {
        return GameContainer.SymbolTable[_name];
    }
    
}
}
