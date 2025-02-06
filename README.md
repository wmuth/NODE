

# NODE
**NODE** is an open source Narrative Oriented Dialogue Engine. It is inspired by "GOTO" style old-school scripting languages from games and designed to have a similar, but improved, syntax. Dialogue is routed through a series of nodes, which can be specified to have functionality useful for writing branching dialogue, ie variable dependant dialogue options or options that can only be used once.

The system is optimised for 1-1 dialogue between players and NPCs. The player is able to make a number of different choices in conversation, and the NPC can be specified to react accordingly. Each NPC therefore corresponds to a "Conversation File" which holds all their interactions with the player (inspired by the system in Fallout 1). An advantage of character files is that they are very readable, and a writer can quickly skim through them and find out all about the NPCs current and potential interactions with the player. Similarly, conversation topics can easily be sorted and categorised into seperate nodes.


#### Features of NODEs syntax include

- Branching dialogue options
- Impact of player conversation choicse on variables
- Options can be specified to be only used once, or only available if a certain requirement is met
- Variables of note can be declared at the top of the conversation file, and saved/remembered for later

## Syntax

## Current syntax of NODE

Not all features in the syntax design are available to use right now. Currently, if you want to create conversation files with NODE the following syntax must be used:


In the top of the conversation file variables can be initialised. **?=** sets a variable if it does not exist.

```csharp
        relationship ?= 5;
```

The **==start==** node is always the first node. The first line is always the speakers line and the lines below are the players available replies. The **=>** option will proceed the conversation to the next node and **=x** will end the conversation. The bracket proceeding the node type can specify if any variable should be checked before presenting the option.

```csharp
==start==
{} {"The thing the speaker is saying"}
=> {} {"The reply used to continue to another note"} -> {continue} {}
=> {relationship == 5} {"This option can be used if the player has a relationship of 5 with the NPC"} -> {continue} {relationship += 5}
=x {"This ends the conversation"}
```
