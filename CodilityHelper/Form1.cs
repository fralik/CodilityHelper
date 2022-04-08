using Microsoft.Playwright;

namespace CodilityHelper
{
    public partial class Form1 : Form
    {
        private class SessionInfo
        {
            public string Url { get; set; }
            public string Code { get; set; }
            public string User { get; set; }
            public string Email { get; set; }

            public SessionInfo(string url, string code, string email, string user)
            {
                Url = url;
                Code = code;
                User = user;
                Email = email;
            }
        }

        public Form1()
        {
            InitializeComponent();
            inpLanguage.SelectedIndex = 0;
        }

        private void outputMessage(string msg)
        {
            txtOutput.Text += msg.Replace("\n", "\r\n");
            txtOutput.Select(txtOutput.Text.Length, 0);
        }

        private async Task<SessionInfo> createCodilitySession(IPage page)
        {
            try
            {
                await page.GotoAsync("https://mscodilityhelper.azurewebsites.net/");
                await page.WaitForSelectorAsync("text=Create CodeLive Whiteboard Session", new PageWaitForSelectorOptions
                {
                    Timeout = 5000
                });
            }
            catch (Exception)
            {
                throw;
            }

            var buttons = page.Locator("button");
            await buttons.Last.TapAsync();
            await page.WaitForSelectorAsync("text=Access Code:");

            var allPTexts = await page.Locator("p").AllInnerTextsAsync();
            var url = "";
            var code = "";
            var user = "";
            var email = "";
            foreach (var item in allPTexts)
            {
                if (item.Contains("URL:"))
                {
                    url = item.Split(" ")[1].Trim();
                }
                if (item.Contains("Access Code:"))
                {
                    code = item.ToLower().Replace("access code:", "").Trim();
                }
                if (item.Contains("Username:"))
                {
                    email = item.ToLower().Replace("username:", "").Trim();
                }
                if (item.Contains("Name:"))
                {
                    user = item.ToLower().Replace("name:", "").Trim();
                }
            }
            return new SessionInfo(url, code, email, user);
        }

        private async Task setRunning(bool isRunning)
        {
            if (isRunning)
            {
                //prgRunning.Visibility = Visibility.Visible;
                btnRun.Enabled = false;
                inpTaskText.Enabled = false;
                inpNumSessions.Enabled = false;
                inpLanguage.Enabled = false;
                Cursor = Cursors.WaitCursor;
                await Task.Delay(100);
            }
            else
            {
                btnRun.Enabled = true;
                // prgRunning.Visibility = Visibility.Collapsed;
                inpTaskText.Enabled = true;
                inpNumSessions.Enabled = true;
                inpLanguage.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private async Task populateSession(IPage page, SessionInfo info, string task, string lang)
        {
            await page.GotoAsync(info.Url);
            await page.FillAsync("#auto-id-1", info.Email);
            await page.FillAsync("#auto-id-2", info.User);
            await page.FillAsync("#auto-id-4", info.Code);
            await page.Locator("button").ClickAsync();

            try
            {
                await page.WaitForSelectorAsync("text=Skip", new PageWaitForSelectorOptions
                {
                    Timeout = 5000,
                });
            }
            catch (Exception)
            {
                // do nothing, perhaps all is fine
            }
            var skip_el = page.Locator("text=Skip");
            if (await skip_el.CountAsync() > 0)
            {
                await skip_el.ClickAsync();
            }
            if (await page.Locator("text=Network error").CountAsync() > 0)
            {
                await page.ReloadAsync();
            }

            await page.WaitForSelectorAsync("#select-variant-technology");

            var maxRetries = 5;
            for (var i = 0; i < maxRetries; i++)
            {
                await page.FillAsync("#editable_task_description", task);
                await Task.Delay(2000);
                var enteredTask = await page.Locator("#editable_task_description").AllTextContentsAsync();
                var firstTask = enteredTask.First().Replace("\r", "").Replace("\n", "");

                if (firstTask.Equals(task.Replace("\r", "").Replace("\n", "")))
                {
                    break;
                }
            }
            await Task.Delay(3000);

            // select programming language
            var allLi = page.Locator("li"); // get all option elements
            var languages = await allLi.AllInnerTextsAsync();
            var idx = languages.ToList().FindIndex(x => x == lang);
            await page.Locator("#select-variant-technology").TapAsync(); // tap on language selection
            await allLi.Nth(idx).TapAsync();
            await Task.Delay(2000);

            await page.Locator("#editable_task_description").ClickAsync();
            
            await page.FillAsync("#editable_task_description", task);
        }

        private async void btnRun_Click(object sender, EventArgs e)
        {
            if (inpTaskText.Text.Length == 0)
            {
                var message = "Please paste in or type the coding challenge.";
                var caption = "Coding challege is missing!";

                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await setRunning(true);
            outputMessage("==== start running ====\n");

            var tempPath = $"{Path.GetTempPath()}/.codilityhelper_context";
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
                    tempPath,
                    new BrowserTypeLaunchPersistentContextOptions
                    {
                        Headless = false,
                        Channel = "msedge",
                        HasTouch = true,
                    }
                );
                IPage page;
                if (context.Pages.Count > 0)
                {
                    page = context.Pages[0];
                }
                else
                {
                    page = await context.NewPageAsync();
                }

                for (int i = 0; i < inpNumSessions.Value; i++)
                {
                    var info = await createCodilitySession(page);
                    outputMessage($"{info.Url}\n{info.Code}\n");
                    await populateSession(page, info, inpTaskText.Text, (string)inpLanguage.SelectedItem);
                }
            }
            catch (Exception ex)
            {
                outputMessage($"Exception: {ex.Message}\n");
            }
            Directory.Delete(tempPath, true);
            await setRunning(false);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                linkLabel1.LinkVisited = true;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"mailto:{linkLabel1.Text}?subject=CodilityHelper") { UseShellExecute = true });
            } catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}