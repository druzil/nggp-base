# nggp-base
A partial port of the ggp-base code-base to .net

This code base is intended as a starting point for students that are taking the Stanford GGP online course.

You can connect to the tiltyard website (once registered) and play against other gamers by executing the run.bat file which
will run the gaming service on port 4001 (ensure that your tiltyard registration matches the port number).

The default gamer is 'RandomGamer', it will make a random legal move.  This can be changed in the GetGamerFactory method of 
Program.cs to another class that implements 'Gamer'.

The PropnetGamer.cs is a starting point for implementing a gamer that uses a propnet.  A few 'TODO' items need to be completed 
for the gamer to run successfully.

When the source code is run in DEBUG mode - it will run a local version of the gamer using one of the game definitions in the 
'game-defs' directory - the default is set to tic-tac-toe.  This allows testing and debugging locally.

When you run the source code in DEBUG mode you will see the console window display 'READY' (that may take some time depending on how
long it takes to initialise your gamer).  

Typing 'play' will cause the gamer to find the next best move.

Once a move has been returned (e.g. for tic-tac-toe, it might return '(mark 3 2)') the state can be advanced by entering the moves for both 
players.  The gamer will now calculate the best move for the position after applying the new moves entered.

e.g. (play foo ((mark 3 2) noop))

The above instruction is to play against the game named 'foo' (for testing this name is irrelevant so anything can be entered) and to apply the move
(mark 3 2) for player 1
and noop for player 2


