using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;


namespace FBS2_W
{
    public partial class SeatSelectionWindow : Window
    {
        private int _flightId;
        private int _userId;
        private TcpClient _client; // Ссылка на существующий TcpClient
        private NetworkStream _stream; // Поток для чтения и записи
        private StreamWriter _writer; // Общий StreamWriter
        private StreamReader _reader; // Общий StreamReader

        public SeatSelectionWindow(int flightId, int userId, TcpClient client)
        {
            InitializeComponent();
            _flightId = flightId;
            _userId = userId;
            _client = client;

            try
            {
                // Инициализация потоков
                _stream = _client.GetStream();
                _writer = new StreamWriter(_stream, Encoding.ASCII, 4096, leaveOpen: true);
                _reader = new StreamReader(_stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, 4096, leaveOpen: true);

                LoadSeats(); // Загрузка доступных мест
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации потоков: {ex.Message}");
            }
        }

        private async void LoadSeats()
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    MessageBox.Show("Ошибка подключения к серверу.");
                    return;
                }

                // Отправляем запрос на получение доступных мест
                var availableSeats = await SendRequestToServer($"GET_SEATS {_flightId}");

                if (string.IsNullOrEmpty(availableSeats))
                {
                    MessageBox.Show("Ответ от сервера пустой.");
                    return;
                }

                if (availableSeats.StartsWith("Seats:"))
                {
                    availableSeats = availableSeats.Substring("Seats:".Length).Trim();
                }

                string[] seats = availableSeats.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(seat => seat.Trim())
                                               .ToArray();

                if (seats.Length == 0)
                {
                    MessageBox.Show("Нет доступных мест для выбранного рейса.");
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SeatsListBox.ItemsSource = seats;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async Task<string> SendRequestToServer(string request)
        {
            try
            {
                if (!_stream.CanWrite)
                {
                    MessageBox.Show("Поток недоступен для записи.");
                    return string.Empty;
                }

                await _writer.WriteLineAsync(request);
                await _writer.FlushAsync();

                string response = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(response))
                {
                    MessageBox.Show("Сервер не отправил ответ.");
                    return string.Empty;
                }

                return response;
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка при чтении данных: {ex.Message}");
                return string.Empty;
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка сокета: {ex.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Неизвестная ошибка: {ex.Message}");
                return string.Empty;
            }
        }

        private async void ConfirmSeatButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedSeat = SeatsListBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedSeat))
            {
                bool success = await BookSeatOnServer(_flightId, selectedSeat);
                if (success)
                {
                    var result = MessageBox.Show("Место забронировано успешно! Хотите получить билет по почте?", "Подтверждение по почте", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        SeatsListBox.Visibility = Visibility.Collapsed;
                        ConfirmSeatButton.Visibility = Visibility.Collapsed;
                        Choose.Visibility = Visibility.Collapsed;

                        EmailInputSection.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        this.Close();
                    }
                }
                else
                {
                    MessageBox.Show("Не удалось забронировать место. Попробуйте снова.");
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите место.");
            }
        }

        private async Task<bool> BookSeatOnServer(int flightId, string seat)
        {
            try
            {
                if (!_stream.CanWrite)
                {
                    MessageBox.Show("Поток недоступен для записи.");
                    return false;
                }

                string request = $"BOOK_SEAT {flightId} {seat}";
                await _writer.WriteLineAsync(request);
                await _writer.FlushAsync();

                string response = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(response))
                {
                    MessageBox.Show("Сервер не отправил ответ.");
                    return false;
                }

                return response.StartsWith("SUCCESS");
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка при чтении данных: {ex.Message}");
                return false;
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка сокета: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Неизвестная ошибка: {ex.Message}");
                return false;
            }
        }

        private void SendTicketButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;

            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Пожалуйста, введите действующий адрес электронной почты.");
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Введенный адрес электронной почты недействителен. Пожалуйста, попробуйте снова.");
                return;
            }

            MessageBox.Show("Билет отправлен на вашу почту!");
            this.Close();
        }

        private bool IsValidEmail(string email)
        {
            var emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
            return emailRegex.IsMatch(email);
        }

        private void CloseConnection()
        {
            _writer?.Close();
            _reader?.Close();
            _stream?.Close();
            _client?.Close();
        }

        ~SeatSelectionWindow()
        {
            CloseConnection();
        }
    }
}
