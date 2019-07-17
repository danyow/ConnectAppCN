using System.Collections.Generic;
using ConnectApp.Models.Model;

namespace ConnectApp.Models.ViewModel {
    public class SearchScreenViewModel {
        public bool searchArticleLoading;
        public bool searchUserLoading;
        public string searchKeyword;
        public List<Article> searchArticles;
        public List<User> searchUsers;
        public int searchArticleCurrentPage;
        public List<int> searchArticlePages;
        public bool searchUserHasMore;
        public Dictionary<string, bool> followMap;
        public List<string> searchArticleHistoryList;
        public List<string> searchUserHistoryList;
        public Dictionary<string, User> userDict;
        public Dictionary<string, Team> teamDict;
        public List<PopularSearch> popularSearchArticleList;
        public List<PopularSearch> popularSearchUserList;
        public List<string> blockArticleList;
        public string currentUserId;
        public bool isLoggedIn;
    }
}