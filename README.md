Github: https://github.com/Lucav17/pa4

URL: lucav17.cloudapp.net


Explanation:

First I worked on PA1, I created a new file called API.php that only accepts exact matches that the user inputs, once the player is found it outputs the information into a JSON string on the page. It works like an API by entering the URL with the player name in it and then grabbing the output. I then worked on PA3, making my crawler more efficient and changing the way I inserted elements into my azure table. Instead of just submitting a title and URL I now broke the Title into separate words, and then inserted a URL and Title with each keyword. Using the PA2 front end, I implemented the same autocomplete but now on pressing the search button It searches for titles inside my table that contains those words and ranks them by how often those words appear in the Title inside the table. Those results are then sent to my dashboard where they are kept track of. If the user also enters a players name, it then queries my PA1 API and returns back the data from my database. I implemented caching by using a dictionary and saving search results to it.
