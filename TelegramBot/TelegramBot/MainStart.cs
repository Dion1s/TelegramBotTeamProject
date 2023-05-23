
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Linq;

namespace TelegtamBot
{
    class MainStart
    {
        private readonly TelegramBotClient _client;
        private readonly List<User> _players;
        private readonly List<int> _messageIds;
        private readonly List<long> _chatIds;
        private readonly List<long> _playerIds;

        private Dictionary<long, DateTime> _registrations;
        private List<string> _locations;
        List<User> spies = new List<User>();
        private readonly Update _update;
        List<string> loca = new List<string>();
        List<PlayerRole> playerRoles = new List<PlayerRole>();
        private bool GameStarted = false;
        private List<string> _victims;
        private Dictionary<string, List<User>> _voteCounts = new Dictionary<string, List<User>>();
        private Timer _voteTimer;


        private bool readytocheck = false;
        private bool readytovote = false;
        private bool readytostart = false;

        public MainStart(string token)
        {
            _client = new TelegramBotClient(token);
            _players = new List<User>();
            _registrations = new Dictionary<long, DateTime>();
            _messageIds = new List<int>();
            _locations = new List<string>();
            _update = new Update();
            _chatIds = new List<long>();
            _playerIds = new List<long>();
            _victims = new List<string>();



            // Read locations from file
            string path = "Locations.txt";
            if (System.IO.File.Exists(path))
            {
                _locations = System.IO.File.ReadAllLines(path).ToList();
            }
            else
            {
                Console.WriteLine("File not found: " + path);
            }


        }


        public void Start()
        {

            loca.Clear();

            _client.StartReceiving(Update, Error);


            Console.ReadLine();

        }

        private void AssignRoles()
        {
            List<string> nonSpyRoles = new List<string>();

            Random rnd = new Random();
            int locationIndex = rnd.Next(0, _locations.Count);
            loca.Add(_locations[locationIndex]);
            string location = loca[0];

            // Assign roles to players
            Random random = new Random();
            int numOfSpies = _players.Count >= 15 ? 2 : 1;

            // Випадковий індекс гравця, якому буде дана роль шпигуна
            int spyIndex = random.Next(_players.Count);

            for (int i = 0; i < _players.Count; i++)
            {
                if (i == spyIndex && numOfSpies > 0)
                {
                    playerRoles.Add(new PlayerRole(_players[i], "Spy"));
                    spies.Add(_players[i]);
                    numOfSpies--;
                    nonSpyRoles.Add("Шпигун");
                }
                else
                {
                    nonSpyRoles.Add("Не шпигун");
                    playerRoles.Add(new PlayerRole(_players[i], "NotSpy"));
                }
            }


            // Send the role to all players
            for (int i = 0; i < _players.Count; i++)
            {
                string role = nonSpyRoles[i];
                string message3 = $"Ваша роль: {role}";

                _client.SendTextMessageAsync(_players[i].Id, message3);
            }

            // Send the location to all players who are not spies
            foreach (var player in _players.Except(spies))
            {
                _client.SendTextMessageAsync(player.Id, $"Місце: {location}");
            }



        }

        private void CheckReg(Message message)
        {
            bool BotRoman = true;
            var chatId1 = message.Chat.Id;
            while (true)
            {
                foreach (var chatId in _registrations.Keys.ToList())
                {
                    if (DateTime.Now > _registrations[chatId].AddSeconds(60))
                    {
                        if (_players.Count < 2)
                        {
                            _players.Clear(); // Очистіть список гравців
                            _registrations.Remove(chatId);
                            _client.SendTextMessageAsync(chatId1, "Час реєстрації закінчився.\nЗамало гравців.");

                            // Delete each message ID in the list
                            foreach (var messageId in _messageIds)
                            {
                                _client.DeleteMessageAsync(chatId, messageId);
                            }
                            // Clear the list of message IDs
                            _messageIds.Clear();


                        }
                        else
                        {
                            if (BotRoman == true)
                            {
                                _registrations.Remove(chatId);
                                var msg = _client.SendTextMessageAsync(chatId1, "Час реєстрації закінчився.\nГра починається!");
                                msg.ContinueWith((task) =>
                                {
                                    Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                                    _client.DeleteMessageAsync(chatId1, task.Result.MessageId);
                                });
                                AssignRoles();
                                GameStarted = true;

                                // Delete each message ID in the list
                                foreach (var messageId in _messageIds)
                                {
                                    _client.DeleteMessageAsync(chatId, messageId);
                                }
                                // Clear the list of message IDs
                                _messageIds.Clear();

                                readytostart = true;
                                if (GameStarted == true)
                                {
                                    _client.SendTextMessageAsync(chatId1, "У вас є 5 хвилин,щоб викрити шпигуна!");
                                    Timer timer = new Timer((state) =>
                                    {
                                        _client.SendTextMessageAsync(chatId1, "Час на гру закінчився!\nНіхто не виграв.");
                                        GameStarted = false;
                                    }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                                }

                                BotRoman = false;
                            }
                        }
                    }
                }
            }
        }

        private void CheckLocation(Message message, Update update)
        {

            // Get the location that was sent by the player
            string playerLocation = update.Message.Text;

            // Get the correct location
            string correctLocation = _locations.FirstOrDefault(x => x.Equals(loca.Last(), StringComparison.OrdinalIgnoreCase));


            // Check if the player is a spy
            bool isSpy = playerRoles.Any(x => x.Player.Id == message.From.Id && x.Role == "Spy");


            if (playerLocation.Equals(correctLocation, StringComparison.OrdinalIgnoreCase))
            {
                if (isSpy)
                {
                    // The spy wins if they guess the location correctly
                    _client.SendTextMessageAsync(message.Chat.Id, "Перемога шпигуна!");
                    GameStarted = false;
                }
            }
            else if (_locations.Any(x => x.Equals(playerLocation, StringComparison.OrdinalIgnoreCase)))
            {
                if (isSpy)
                {
                    // Player guessed a valid location that is not the correct one
                    _client.SendTextMessageAsync(message.Chat.Id, "Невірна локація! Шпигун програв");
                    GameStarted = false;
                }
            }
        }

        private async Task Kick2(Message message, Update update)
        {
            try
            {
                // Get all players' names and assign a numerical value to each
                var players = _players.Select((player, index) => new { Name = player.FirstName, Value = index }).ToList();

                // Create rows of buttons with each player's name
                var buttonRows = players.Select(x => new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(x.Name) }).ToList();

                // Create a keyboard from the button rows
                var keyboard = new InlineKeyboardMarkup(buttonRows);

                // Send the message with the keyboard
                var sentMessage = await _client.SendTextMessageAsync(message.Chat.Id, "Оберіть учасника:", replyMarkup: keyboard);

                // Store the message ID for later deletion
                _messageIds.Add(sentMessage.MessageId);




                _voteTimer = new Timer((state) =>
                {
                    _client.DeleteMessageAsync(message.Chat.Id, sentMessage.MessageId);
                    // Check if there are any votes
                    if (_voteCounts.Count == 0)
                    {
                        _client.SendTextMessageAsync(message.Chat.Id, "Немає голосів. Потрібно проголосувати.");
                        return;
                    }

                    // Determine the most voted player
                    var mostVotedEntry = _voteCounts.OrderByDescending(x => x.Value.Count).FirstOrDefault();
                    var mostVotedPlayer = mostVotedEntry.Key;


                    if (mostVotedPlayer == null)
                    {
                        throw new Exception($"Error: Failed to find a player with the name {mostVotedPlayer}.");
                    }

                    var isTie = _voteCounts.Count(x => x.Value.Count == mostVotedEntry.Value.Count) > 1;

                    if (isTie)
                    {
                        _client.SendTextMessageAsync(message.Chat.Id, "Думки розійшлися. Давайте ще раз.");
                        _voteCounts.Clear();

                    }
                    else
                    {
                        if (spies.Contains(_players.FirstOrDefault(player => player.FirstName == mostVotedPlayer)))
                        {
                            _client.SendTextMessageAsync(message.Chat.Id, "Шпигун програв! Його розсекретили! ");
                            GameStarted = false;
                        }
                        else
                        {
                            _client.SendTextMessageAsync(message.Chat.Id, "Шпигун виграв! Його не знайшли!");
                            GameStarted = false;
                        }


                    }

                }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);

            }
            catch (Exception ex)
            {
                // Handle exceptions
                var errorMessage = $"Сталася помилка: {ex.Message}";
                await _client.SendTextMessageAsync(message.Chat.Id, errorMessage);
            }
        }

        private async Task ExtendRegistration(Message message)
        {
            var chatId = message.Chat.Id;
            if (_registrations.ContainsKey(message.From.Id))
            {
                // Оновлюємо значення існуючого запису з ключем message.From.Id
                _registrations[message.From.Id] = _registrations[message.From.Id].AddSeconds(30);
            }
            else
            {
                // Додаємо новий запис з ключем message.From.Id
                _registrations.Add(message.From.Id, DateTime.Now.AddSeconds(30));
            }

            var msg = await _client.SendTextMessageAsync(chatId, "Час реєстрації продовжено на 30 секунд!");
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _client.DeleteMessageAsync(chatId, msg.MessageId);


        }

        private async Task Locations(Message message)
        {
            var messages = System.IO.File.ReadAllLines("Locations.txt");
            var combinedMessage = string.Join(Environment.NewLine, messages);
            var fullMessage = $"*Наші локації*\n{combinedMessage}";
            await _client.SendTextMessageAsync(message.From.Id, fullMessage, parseMode: ParseMode.Markdown);
        }

        private async void Restric(Message message, Update update)
        {
            // Check if the message is sent by a player who is not in the game
            if (!_players.Any(x => x.Id == (message?.From?.Id ?? update?.CallbackQuery?.From?.Id)))
            {
                if (update?.CallbackQuery != null)
                {
                    // If the message is a callback query, answer with a message and delete the query message
                    await _client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Ви не берете участі в грі!");
                    await _client.DeleteMessageAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId);
                }
                else if (message != null)
                {
                    // If the message is a regular message, delete the message
                    await _client.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                }
            }
        }

        public class PlayerRole
        {
            public User Player { get; set; }
            public string Role { get; set; }


            public PlayerRole(User player, string role)
            {
                Player = player;
                Role = role;

            }
        }

        private async Task StartTheGame(Message message)
        {
            var replyMarkup = new InlineKeyboardMarkup(new[]
            {new[] { InlineKeyboardButton.WithCallbackData("Приєднатися до гри", "join_game") }});


            var replyMarkup1 = new InlineKeyboardMarkup(new[]
            {new[] { InlineKeyboardButton.WithUrl("Connect", $"https://t.me/newspygame_bot?start={"join_game"}") }});

            var msg1 = await _client.SendTextMessageAsync(
                message.Chat.Id,
                "Привіт! Я бот для гри Шпигун. Щоб приєднатися до гри, натисни кнопку нижче.",
                replyMarkup: replyMarkup1
                                                  );


            _messageIds.Add(msg1.MessageId);
            _chatIds.Add(msg1.Chat.Id);
        }

        private async Task StartOneMore(Message message, ITelegramBotClient botClient)
        {
            if (!_players.Any(player => player.Id == message.From.Id) && _chatIds.Count > 0 && _messageIds.Count > 0)
            {
                _players.Add(message.From);
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("<b>Проводиться набір у гру!</b>");
                messageBuilder.AppendLine("\nЗареєстровані гравці:");
                foreach (var player in _players)
                {
                    messageBuilder.AppendLine($"<a href='tg://user?id={player.Id}'>{player.FirstName}</a>");
                }
                var msg2 = await botClient.EditMessageTextAsync(_chatIds[0], _messageIds[0], messageBuilder.ToString()
                    + $"Кількість гравців: {_players.Count} ",
                   replyMarkup: new InlineKeyboardMarkup(new[] { new[]
                        { InlineKeyboardButton.WithUrl("Connect", $"https://t.me/newspygame_bot?start={"join_game"}") } }), parseMode: ParseMode.Html);

                _registrations.TryAdd(message.From.Id, DateTime.Now.AddSeconds(1));
                _messageIds.Add(msg2.MessageId);

            }
        }

        private async Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            var message = update.Message;

            if (update.CallbackQuery != null)
            {

                // Get the CallbackQuery object from the Update object
                var callbackQuery = update.CallbackQuery;
                Console.WriteLine("CALLBACK");
                var data = callbackQuery.Data;
                Console.WriteLine(data);

                if (!_players.Any(x => x.Id == (update?.CallbackQuery?.From?.Id)) || !_players.Any(x => x.Id == (update?.CallbackQuery?.From?.Id)) && readytovote == false)
                {
                    // If the message is a callback query, answer with a message and delete the query message
                    await _client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Ви не берете участі в грі!");
                }
                else
                {
                    if (update.CallbackQuery.From.FirstName == data)
                    {
                        await _client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Ви не можете голосувати за себе!");
                        return;
                    }

                    // Check if the voter has already voted for another player
                    foreach (var voteList in _voteCounts.Values)
                    {
                        if (voteList.Any(voter => voter.Id == update.CallbackQuery.From.Id))
                        {
                            await _client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Ви вже проголосували!");
                            return;
                        }
                    }

                    _voteCounts[data] = new List<User> { update.CallbackQuery.From };
                    await _client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, $"Ваш голос зараховано.");
                }



            }



            if (update.Message != null && update.Message.Text != null)
            {
                if (message.Text != null)
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.WriteLine($"{message.Chat.Type} | {message.Chat.Title} | {message.From.Username} | {message.From.FirstName} : {message.Text}");



                    if (GameStarted == true)
                    {

                        CheckLocation(message, update);
                        Restric(message, update);

                        if (message.Text.StartsWith("/vote") && readytovote == true)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Голосування вже розпочате!");
                            await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                        }


                        if (message.Text.StartsWith("/vote"))
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Починається голосування!У вас 30 секунд! ");

                            await Task.Delay(TimeSpan.FromSeconds(1));
                            await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                            await Kick2(message, update);
                            readytovote = true;
                        }



                    }
                    else if (GameStarted == false && message.Text.StartsWith("/vote"))
                    {
                        var msg = await botClient.SendTextMessageAsync(message.Chat.Id, "Куди спішиш? Гра ще не почалась.");

                        await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);

                        await Task.Delay(TimeSpan.FromSeconds(4));
                        await botClient.DeleteMessageAsync(message.Chat.Id, msg.MessageId);

                    }
                    if (readytocheck == true)
                    {
                        if (message.Text.StartsWith("/extend") && message.Chat.Type == ChatType.Supergroup || message.Chat.Type == ChatType.Group)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                            await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                            await ExtendRegistration(message);
                        }
                    }
                    else if (readytocheck == false && message.Text.StartsWith("/extend"))
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Куди спішиш? Гра ще не почалась.");

                    }

                    //commands
                    if (message.Text.StartsWith("/game") && message.Chat.Type == ChatType.Supergroup || message.Chat.Type == ChatType.Group)
                    {

                        //await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);


                        // Clear the lists
                        _players.Clear();
                        _registrations.Clear();
                        playerRoles.Clear();
                        spies.Clear();
                        loca.Clear();
                        _victims.Clear();
                        _chatIds.Clear();
                        _messageIds.Clear();


                        await StartTheGame(message);

                        Task.Run(() => CheckReg(message));

                        GameStarted = false;
                        readytocheck = true;

                    }
                    if (message.Text.StartsWith("/game") && readytocheck == true)
                    {
                        await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                    }

                    if (message.Text.StartsWith("/start") && message.Chat.Type == ChatType.Private)
                    {
                        if (!_players.Any(player => player.Id == message.From.Id) && _chatIds.Count > 0 && _messageIds.Count > 0)
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Ти в ігрі ,друже!", replyToMessageId: message.MessageId);

                            await StartOneMore(message, botClient);

                        }
                        else { await botClient.SendTextMessageAsync(message.Chat.Id, "Ти вже в ігрі!", replyToMessageId: message.MessageId); }
                    }
                    if (message.Text.StartsWith("/start") && message.Chat.Type == ChatType.Private && readytostart == true)
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Вже пізно!", replyToMessageId: message.MessageId);
                    }




                    if (message.Text.StartsWith("/locations"))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        await botClient.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                        await Locations(message);
                    }


                }
            }
        }

        private static Task Error(ITelegramBotClient botClient, Exception update, CancellationToken token)
        {
            Console.WriteLine($"Error: {update.Message}");
            return Task.CompletedTask;
        }


        static void Main(string[] args)
        {
            var program = new MainStart("6038898470:AAHJd_RMQAmmpcop8w8Oa085ebzhNUr2gj4");
            program.Start();
        }
    }
}