using System.Collections.Generic;
using System.Linq;
using ConnectApp.Components;
using ConnectApp.Components.pull_to_refresh;
using ConnectApp.Constants;
using ConnectApp.Main;
using ConnectApp.Models.ActionModel;
using ConnectApp.Models.Model;
using ConnectApp.Models.State;
using ConnectApp.Models.ViewModel;
using ConnectApp.redux.actions;
using ConnectApp.Utils;
using RSG;
using Unity.UIWidgets.foundation;
using Unity.UIWidgets.painting;
using Unity.UIWidgets.Redux;
using Unity.UIWidgets.rendering;
using Unity.UIWidgets.ui;
using Unity.UIWidgets.widgets;

namespace ConnectApp.screens {
    public class MessengerScreenConnector : StatelessWidget {
        public MessengerScreenConnector(
            Key key = null
        ) : base(key: key) {
        }

        public override Widget build(BuildContext context) {
            return new StoreConnector<AppState, MessengerScreenViewModel>(
                converter: state => {
                    var joinedChannels = state.channelState.joinedChannels.Select(
                        channelId => {
                            ChannelView channel = state.channelState.channelDict[key: channelId];
                            channel.isTop = state.channelState.channelTop.TryGetValue(channelId, out var isTop) &&
                                            isTop;
                            return channel;
                        }).ToList();
                    joinedChannels.Sort(
                        (c1, c2) => {
                            if (c1.isTop && !c2.isTop) {
                                return -1;
                            }

                            if (!c1.isTop && c2.isTop) {
                                return 1;
                            }

                            return (c2.lastMessage.time - c1.lastMessage.time).Milliseconds;
                        });
                    var lastMessageMap = new Dictionary<string, string>();
                    foreach (var channel in joinedChannels) {
                        if (!string.IsNullOrEmpty(value: channel.lastMessageId)) {
                            lastMessageMap[key: channel.id] = channel.lastMessageId;
                        }
                    }

                    return new MessengerScreenViewModel {
                        joinedChannels = joinedChannels,
                        lastMessageMap = lastMessageMap,
                        hasUnreadNotifications = state.channelState.newNotifications != null,
                        popularChannels = state.channelState.publicChannels
                            .Select(channelId => state.channelState.channelDict[key: channelId])
                            .Take(state.channelState.publicChannels.Count > 0
                                ? 8
                                : state.channelState.publicChannels.Count)
                            .ToList(),
                        publicChannels = state.channelState.publicChannels
                            .Select(channelId => state.channelState.channelDict[key: channelId])
                            .Take(joinedChannels.Count > 0
                                ? 8
                                : state.channelState.publicChannels.Count)
                            .ToList(),
                        currentTabBarIndex = state.tabBarState.currentTabIndex
                    };
                },
                builder: (context1, viewModel, dispatcher) => {
                    var actionModel = new MessengerScreenActionModel {
                        pushToNotifications = () => {
                            dispatcher.dispatch(new MainNavigatorPushToAction {
                                routeName = MainNavigatorRoutes.Notification
                            });
                            dispatcher.dispatch(new UpdateNewNotificationAction {
                                notification = null
                            });
                        },
                        pushToDiscoverChannels = () => dispatcher.dispatch(new MainNavigatorPushToAction {
                            routeName = MainNavigatorRoutes.DiscoverChannel
                        }),
                        updateNewNotification = () => dispatcher.dispatch(new UpdateNewNotificationAction {
                            notification = ""
                        }),
                        pushToChannel = channelId => {
                            dispatcher.dispatch(new MainNavigatorPushToChannelAction {
                                channelId = channelId
                            });
                            if (viewModel.lastMessageMap.TryGetValue(key: channelId, out var messageId)) {
                                dispatcher.dispatch(Actions.ackChannelMessage(messageId: messageId));
                            }
                        },
                        pushToChannelDetail = channelId => dispatcher.dispatch(
                            new MainNavigatorPushToChannelDetailAction {
                                channelId = channelId
                            }),
                        fetchChannels = pageNumber =>
                            dispatcher.dispatch<IPromise>(Actions.fetchChannels(page: pageNumber)),
                        startJoinChannel = channelId => dispatcher.dispatch(new StartJoinChannelAction {
                            channelId = channelId
                        }),
                        joinChannel = (channelId, groupId) =>
                            dispatcher.dispatch<IPromise>(Actions.joinChannel(channelId: channelId, groupId: groupId))
                    };
                    return new MessengerScreen(viewModel: viewModel, actionModel: actionModel);
                }
            );
        }
    }

    public class MessengerScreen : StatefulWidget {
        public MessengerScreen(
            MessengerScreenViewModel viewModel = null,
            MessengerScreenActionModel actionModel = null,
            Key key = null
        ) : base(key: key) {
            this.viewModel = viewModel;
            this.actionModel = actionModel;
        }

        public readonly MessengerScreenViewModel viewModel;
        public readonly MessengerScreenActionModel actionModel;

        public override State createState() {
            return new _MessageScreenState();
        }
    }

    public class _MessageScreenState : AutomaticKeepAliveClientMixin<MessengerScreen>, RouteAware {
        RefreshController _refreshController;
        int _pageNumber;
        string _newNotificationSubId;

        protected override bool wantKeepAlive {
            get { return true; }
        }

        public override void initState() {
            base.initState();
            this._refreshController = new RefreshController();
            this._pageNumber = 1;
            this._newNotificationSubId = EventBus.subscribe(sName: EventBusConstant.newNotifications, args => {
                this.widget.actionModel.updateNewNotification();
            });
        }

        public override void didChangeDependencies() {
            base.didChangeDependencies();
            Router.routeObserve.subscribe(this, (PageRoute) ModalRoute.of(context: this.context));
        }

        public override void dispose() {
            Router.routeObserve.unsubscribe(this);
            EventBus.unSubscribe(sName: EventBusConstant.newNotifications, id: this._newNotificationSubId);
            base.dispose();
        }

        bool _hasJoinedChannel() {
            return this.widget.viewModel.joinedChannels.isNotEmpty();
        }

        public override Widget build(BuildContext context) {
            base.build(context: context);
            return new Container(
                padding: EdgeInsets.only(top: CCommonUtils.getSafeAreaTopPadding(context: context)),
                color: CColors.White,
                child: new Column(
                    children: new List<Widget> {
                        this._buildNavigationBar(),
                        new Container(color: CColors.Separator2, height: 1),
                        new Flexible(
                            child: new NotificationListener<ScrollNotification>(
                                child: new Container(
                                    color: CColors.Background,
                                    child: new SectionView(
                                        controller: this._refreshController,
                                        enablePullDown: true,
                                        enablePullUp: false,
                                        onRefresh: this._onRefresh,
                                        hasBottomMargin: true,
                                        sectionCount: 2,
                                        numOfRowInSection: section => {
                                            if (section == 0) {
                                                return !this._hasJoinedChannel()
                                                    ? 1
                                                    : this.widget.viewModel.joinedChannels.Count;
                                            }

                                            return this.widget.viewModel.publicChannels.Count;
                                        },
                                        headerInSection: this._headerInSection,
                                        cellAtIndexPath: this._buildMessageItem,
                                        footerWidget: new EndView(hasBottomMargin: true)
                                    )
                                )
                            )
                        )
                    }
                )
            );
        }

        Widget _buildNavigationBar() {
            return new CustomNavigationBar(
                new Text("群聊", style: CTextStyle.H2),
                new List<Widget> {
                    new CustomButton(
                        onPressed: () => this.widget.actionModel.pushToNotifications(),
                        padding: EdgeInsets.symmetric(8, 16),
                        child: new Container(
                            width: 28,
                            height: 28,
                            child: new Stack(
                                children: new List<Widget> {
                                    new Icon(
                                        icon: Icons.outline_notification,
                                        color: CColors.Icon,
                                        size: 28
                                    ),
                                    Positioned.fill(
                                        new Align(
                                            alignment: Alignment.topRight,
                                            child: new NotificationDot(
                                                this.widget.viewModel.hasUnreadNotifications ? "" : null,
                                                new BorderSide(color: CColors.White, 2)
                                            )
                                        )
                                    )
                                }
                            )
                        )
                    )
                },
                backgroundColor: CColors.White,
                0,
                EdgeInsets.only(16, bottom: 8)
            );
        }

        Widget _headerInSection(int section) {
            if (section == 0) {
                return null;
            }

            if (this.widget.viewModel.publicChannels.isEmpty()) {
                return null;
            }

            Widget rightWidget;
            if (!this._hasJoinedChannel()) {
                rightWidget = new Container();
            }
            else {
                rightWidget = new GestureDetector(
                    onTap: () => this.widget.actionModel.pushToDiscoverChannels(),
                    child: new Container(
                        color: CColors.Transparent,
                        child: new Row(
                            children: new List<Widget> {
                                new Padding(
                                    padding: EdgeInsets.only(top: 2),
                                    child: new Text(
                                        "查看全部",
                                        style: new TextStyle(
                                            fontSize: 12,
                                            fontFamily: "Roboto-Regular",
                                            color: CColors.TextBody4
                                        )
                                    )
                                ),
                                new Icon(
                                    icon: Icons.chevron_right,
                                    size: 20,
                                    color: Color.fromRGBO(199, 203, 207, 1)
                                )
                            }
                        )
                    )
                );
            }

            return new Container(
                child: new Column(
                    children: new List<Widget> {
                        !this._hasJoinedChannel()
                            ? new Container(height: 24, color: CColors.White)
                            : new Container(height: 16),
                        new Container(
                            color: CColors.White,
                            padding: !this._hasJoinedChannel()
                                ? EdgeInsets.all(16)
                                : EdgeInsets.only(16, 16, 8, 16),
                            child: new Row(
                                children: new List<Widget> {
                                    new Text("发现群聊", style: CTextStyle.H5),
                                    new Expanded(
                                        child: new Container()
                                    ),
                                    rightWidget
                                }
                            )
                        )
                    }
                )
            );
        }

        Widget _buildMessageItem(BuildContext context, int section, int row) {
            var joinedChannels = this.widget.viewModel.joinedChannels;
            if (section == 0) {
                if (!this._hasJoinedChannel()) {
                    return new Container(
                        color: CColors.White,
                        child: new Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: new List<Widget> {
                                new Container(
                                    padding: EdgeInsets.only(16, right: 16, top: 20),
                                    child: new Text("热门群聊", style: CTextStyle.H5)
                                ),
                                new Container(
                                    padding: EdgeInsets.only(top: 16),
                                    color: CColors.White,
                                    child: new SingleChildScrollView(
                                        scrollDirection: Axis.horizontal,
                                        child: new Container(
                                            padding: EdgeInsets.only(16),
                                            child: new Row(
                                                children: this.widget.viewModel.popularChannels.Select(
                                                    popularChannel => (Widget) new PopularChannelCard(
                                                        channel: popularChannel,
                                                        () => this.widget.actionModel.pushToChannelDetail(
                                                            obj: popularChannel.id)
                                                    )
                                                ).ToList()
                                            )
                                        )
                                    )
                                )
                            }
                        )
                    );
                }

                var joinedChannel = joinedChannels[index: row];
                return new JoinedChannelCard(
                    channel: joinedChannel,
                    () => this.widget.actionModel.pushToChannel(obj: joinedChannel.id)
                );
            }

            var publicChannel = this.widget.viewModel.publicChannels[index: row];
            return new DiscoverChannelCard(
                channel: publicChannel,
                () => {
                    if (publicChannel.joined) {
                        this.widget.actionModel.pushToChannel(obj: publicChannel.id);
                    }
                    else {
                        this.widget.actionModel.pushToChannelDetail(obj: publicChannel.id);
                    }
                },
                () => {
                    this.widget.actionModel.startJoinChannel(obj: publicChannel.id);
                    this.widget.actionModel.joinChannel(arg1: publicChannel.id, arg2: publicChannel.groupId);
                }
            );
        }

        public void didPopNext() {
            if (this.widget.viewModel.currentTabBarIndex == 2) {
                StatusBarManager.statusBarStyle(false);
            }
        }

        public void didPush() {
        }

        public void didPop() {
        }

        public void didPushNext() {
        }

        void _onRefresh(bool up) {
            if (up) {
                this._pageNumber = 1;
            }
            else {
                this._pageNumber++;
            }

            this.widget.actionModel.fetchChannels(arg: this._pageNumber)
                .Then(() => this._refreshController.sendBack(up: up, up ? RefreshStatus.completed : RefreshStatus.idle))
                .Catch(e => this._refreshController.sendBack(up: up, mode: RefreshStatus.failed));
        }
    }
}