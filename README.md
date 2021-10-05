# Matrix Chat
Matrix themed TUI chat application (CTF challenge).

## Features:
  - Join rooms
  - Write private messages
  - Toggle display of message time
  - View your message history

### Usage: 
Start the server's executable from the *bin* folder. Join the server via provided client.

### Solution:
Using regex bypass tecnique we can obtain an arbitrary file read.

For example, to get `../Server.deps.json` we write `./...//..Server.deps.json` as our username.

Then we execute `/memories` on the server to read our chat history. But the file `../Server.deps.json` is printed instead.

An automated exploit is available [here](https://github.com/Azarattum/MatrixChat/blob/master/exploit.py). Note that `pywinauto` package is required in order to run this automation.