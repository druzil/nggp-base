using System;
using System.Collections.Generic;

namespace nJocLogic.propNet.factory
{
    using architecture;
    using converter;
    using data;
    using flattener;
    using gameContainer;
    using NLog;

    /// <summary>
    /// The PropNetFactory class defines the creation of PropNets from game descriptions.
    /// </summary>
    public sealed class PropNetFactory
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.propnet.factory");

        /// <summary>
        /// Creates a PropNet from a game description using the following process:
        /// <ol>
        /// <li>Flattens the game description to remove variables.</li>
        /// <li>Converts the flattened description into an equivalent PropNet.</li>
        /// </ol>
        /// </summary>
        /// <param name="description">A game description.</param>
        /// <param name="componentFactory"></param>
        /// <returns>An equivalent PropNet.</returns>
        public static PropNet Create(List<Expression> description, IComponentFactory componentFactory)
        {
            try
            {
                List<Implication> flatDescription = new PropNetFlattener(description).Flatten();
                Logger.Info("Converting...");
                return new PropNetConverter(componentFactory).Convert(GameContainer.GameInformation.GetRoles(), flatDescription);
            }
            catch (Exception e)
            {
                Logger.LogException(LogLevel.Error, "Error in propnet.factory", e);
                return null;
            }
        }
    }
}