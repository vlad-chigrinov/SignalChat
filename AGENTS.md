# SignalChat

## Stack

To be decided with the agent

Вы описали проект с онлайн-чатом, комнатами и личными сообщениями, а также входом через Google, с бэкендом на ASP.NET Core и фронтендом на Vue 3. Такой стек не входит в стандартные варианты, поэтому выбран "other" для создания структуры с dotnet 10 и Vue 3, чтобы обеспечить нужную функциональность и локальную разработку.

Do not change the stack without being asked. It was chosen with the user before
the first line of code.

## Security rules

- Аутентификация через Google OAuth должна быть реализована безопасно, с хранением токенов только в памяти или защищённых куках.
- Все сообщения в чатах должны передаваться по защищённому протоколу (например, HTTPS или WSS).
- Доступ к комнатам и личным чатам должен контролироваться с проверкой прав пользователя на сервере.
- Личные данные пользователей не должны сохраняться в открытом виде и должны быть защищены согласно стандартам конфиденциальности.

## Run contract

Document the single command that runs this project in the README as soon as it exists.

## Conventions

- Small files, one responsibility each. Split a file before it becomes hard to read.
- Explicit names over clever ones. No magic numbers.
- Never commit a secret. Anything sensitive goes in `.env` and stays out of git.
- Say what you are about to do in two lines before writing code.
