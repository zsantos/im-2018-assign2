﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using mmisharp;
using Newtonsoft.Json;
using SpotifyAPI.Local; //Base Namespace
using SpotifyAPI.Local.Enums; //Enums
using SpotifyAPI.Local.Models; //Models for the JSON-responses
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using SpotifyAPI.Web;
using System.Collections.Generic;
using multimodal;

namespace AppGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MmiCommunication mmiC;
        private static SpotifyLocalAPI spotify;
        private SpotifyWebAPI webSpotify;
        private Tts t = new Tts();
        public MainWindow()
        {
            InitializeComponent();

            t.Speak("Wait a few seconds, we are just getting everything ready for you!");
            SpotifyAPI spotifyAPI = new SpotifyAPI();
            webSpotify = spotifyAPI.getAPI();
            spotify = new SpotifyLocalAPI(new SpotifyLocalAPIConfig
            {
                Port = 4381,
                HostUrl = "http://localhost"
            });
            if (!SpotifyLocalAPI.IsSpotifyRunning())
            {
                //MessageBox.Show("Spotify is not running");
                t.Speak("Spotify is not running, can you please turn it on ?");
                return; //Make sure the spotify client is running
            }
            if (!SpotifyLocalAPI.IsSpotifyWebHelperRunning())
            {
                //MessageBox.Show("Spotify WebHelper is not running");
                t.Speak("Spotify WebHelper is not running");
                return; //Make sure the WebHelper is running
            }

            if (!spotify.Connect())
            {
                //MessageBox.Show("Spotify is not connected");
                t.Speak("Spotify is not connected.");
                return; //We need to call Connect before fetching infos, this will handle Auth stuff
            }

            StatusResponse status = spotify.GetStatus(); //status contains infos
            t.Speak("You can close the browser now, hope you have a good time with our application");
            
            mmiC = new MmiCommunication("localhost", 8000, "User1", "GUI");
            mmiC.Message += MmiC_Message;
            mmiC.Start();
        }

        private void MmiC_Message(object sender, MmiEventArgs e)
        {
            Console.WriteLine(e.Message);
            var doc = XDocument.Parse(e.Message);
            var com = doc.Descendants("command").FirstOrDefault().Value;
            dynamic json = JsonConvert.DeserializeObject(com);
            String command = (string)json.recognized[0].ToString();
            String album = (string)json.recognized[1].ToString();
            String song_1 = (string)json.recognized[2].ToString();
            String by = (string)json.recognized[3].ToString();
            String artist = (string)json.recognized[4].ToString();
            String song_2 = (string)json.recognized[5].ToString();
            String genre = (string)json.recognized[6].ToString();
            String from = (string)json.recognized[7].ToString();
            String year = (string)json.recognized[8].ToString();
            SearchItem item;
            float volume;

            if (t.getSpeech() == true)
            {
                MessageBox.Show("Speaking");
                return;
            }

            // Using just a normal command
            switch (command)
            {
                case "HELP":
                    t.Speak("You can play or stop music, skip music, listen to previous music, I wanna listen Queen");
                    break;
                case "PLAY":
                    spotify.Play();
                    break;
                case "PAUSE":
                    spotify.Pause();
                    break;
                case "SKIP":
                    spotify.Skip();
                    break;
                case "BACK":
                    spotify.Previous();
                    spotify.Previous();
                    break;
                case "VDOWN":
                    volume = spotify.GetSpotifyVolume();
                    if (volume - 25 >= 0)
                        spotify.SetSpotifyVolume(volume - 25);
                    else
                        spotify.SetSpotifyVolume(0);
                    break;
                case "VUP":
                    volume = spotify.GetSpotifyVolume();
                    if (volume + 25 <= 100)
                        spotify.SetSpotifyVolume(volume + 25);
                    else
                        spotify.SetSpotifyVolume(100);
                    break;
                case "MUTE":
                    if (!spotify.IsSpotifyMuted())
                        spotify.Mute();
                    break;
                case "UNMUTE":
                    if (spotify.IsSpotifyMuted())
                        spotify.UnMute();
                    break;
                case "PLAYLIST":
                    String playlist = webSpotify.GetUserPlaylists(userId: "4lzrg4ac5nyj1f5bosl1pse1i").Items[0].Uri;
                    spotify.PlayURL(playlist);
                    break;

                case "ADD":
                    String playlist1 = webSpotify.GetUserPlaylists(userId: "4lzrg4ac5nyj1f5bosl1pse1i").Items[0].Id;
                    Paging<PlaylistTrack> p = webSpotify.GetPlaylistTracks("4lzrg4ac5nyj1f5bosl1pse1i", playlist1);
                    for (var i = 0; i < p.Items.Count; i++)
                    {
                        if (p.Items[i].Track.Name.Equals(spotify.GetStatus().Track.TrackResource.Name))
                        {
                            //MessageBox.Show("Music already in playlist");
                            
                            t.Speak("This music is already in your playlist");
                            return;
                        }
                    }

                    ErrorResponse x = webSpotify.AddPlaylistTrack("4lzrg4ac5nyj1f5bosl1pse1i", playlist1, spotify.GetStatus().Track.TrackResource.Uri);
                    if (!x.HasError())
                    {
                        t.Speak("This music was added to your playlist");
                    }
                    break;
                /*default:
                    MessageBox.Show("Command not supported");
                    break;*/
            }

            if (command == "LISTEN")
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (by == "BY")
                    {
                        // I wanna listen {album} by {artist}
                        if (album != "EMP" && song_1 == "EMP")
                        {
                            String query = album + "+" + artist;
                            item = webSpotify.SearchItems(query, SearchType.Album);
                            spotify.PlayURL(item.Albums.Items[0].Uri);
                        }
                        // I wanna listen {song} by {artist}
                        else if (song_1 != "EMP" && album == "EMP")
                        {
                            String query = song_1 + "+" + artist;
                            item = webSpotify.SearchItems(query, SearchType.Track);
                                spotify.PlayURL(item.Tracks.Items[0].Uri);
                        }
                    }
                    else {
                        // I wanna listen {artist}
                        if (artist != "EMP" && from == "EMP")
                        {
                            switch (artist)
                            {
                                case "SOMETHING":
                                    spotify.Play();
                                    break;
                                default:
                                    item = webSpotify.SearchItems(artist, SearchType.Artist);
                                        spotify.PlayURL(item.Artists.Items[0].Uri);
                                    break;
                            }
                        }
                        // I wanna listen {artist} from {year}
                        else if(artist != "EMP" && from != "EMP")
                        {
                            item = webSpotify.SearchItems(artist, SearchType.Artist | SearchType.Album);
                            
                            foreach (SimpleAlbum simple_album in item.Albums.Items)
                            {
                                String [] album_date = simple_album.ReleaseDate.Split('-');
                                if (album_date[0] == year)
                                {
                                    spotify.PlayURL(simple_album.Uri);
                                    return;
                                }
                            }
                            t.Speak("There is no album from that artist on that year");
                        }
                        // I wanna listen {song}
                        else if (artist == "EMP" && genre == "EMP" && song_2 != "EMP") {
                            item = webSpotify.SearchItems(song_2, SearchType.Track);
                            spotify.PlayURL(item.Tracks.Items[0].Uri);
                        }
                        // I wanna listen {genre}
                        else if (artist == "EMP" && genre != "EMP" && song_2 == "EMP")
                        {
                            item = webSpotify.SearchItems(genre, SearchType.Album | SearchType.Track | SearchType.Playlist);
                            int results_size = item.Playlists.Items.Count;
                            // Choose playlist randomly
                            Random random = new Random();
                            int index = random.Next(0, results_size);
                            spotify.PlayURL(item.Playlists.Items[index].Uri);
                        }
                        else
                        {
                            t.Speak("I am very sorry but this command is not supported");
                        }
                    }
                });
            }
        }
    }
}
