export default {
    name: 'home',
    template: /*html*/`
    <b-container class="mt-3" fluid>
        <b-navbar toggleable="lg">
            <b-navbar-brand href="#"><h2><strong>NW Market</strong></h2></b-navbar-brand>
            <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>

            <b-collapse id="nav-collapse" is-nav>
                <b-navbar-nav>
                    <b-nav-text><strong>Server</strong></b-nav-text>
                    <b-nav-item-dropdown :text="server">
                        <b-dropdown-item v-for="server in serverList" :key="server" :href="'#/' + server" @click="changeServer(server)">{{server}}</b-dropdown-item>
                    </b-nav-item-dropdown>
                </b-navbar-nav>
            </b-collapse>
        </b-navbar>
        <b-card class="bg-light">
            <p><em>View market listings, recipes, and more to come!</em></p>
            <p>Interesting in contributing to the project? Check out the code on <b-link href="https://github.com/kirchner-trevor/NW_Market_OCR">GitHub</b-link>!</p>
            <p>You can also download the market collector to help keep the site up to date!</p>
            <b-button variant="primary" href="/NW_Market_Collector.zip" download>Download</b-button>
        </b-card>
        </br>
        <b-card no-body>
            <b-tabs card>
                <b-tab  @click="loadMarketData" lazy>
                    <template #title>
                        Listings <b-badge v-if="marketDataLoaded">{{marketData.Listings.length}}</b-badge><b-spinner type="border" v-if="!marketDataLoaded && marketDataRequested" small></b-spinner>
                    </template>
                    <b-navbar toggleable="lg">
                        <b-navbar-brand href="#">Search</b-navbar-brand>
                        <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>
                        <b-collapse id="nav-collapse" is-nav>
                            <b-navbar-nav>
                                <b-input-group>
                                    <b-form-input
                                        id="filter-input"
                                        v-model="filter"
                                        type="search"
                                        placeholder="Type to Search"
                                        debounce="500"
                                    ></b-form-input>

                                    <b-input-group-append>
                                        <b-button :disabled="!filter" @click="filter = ''">Clear</b-button>
                                    </b-input-group-append>
                                </b-input-group>
                            </b-navbar-nav>
                            <b-navbar-nav class="ml-auto">
                                <b-nav-text>Updated <em>{{marketDataUpdated}}</em></b-nav-text>
                            </b-navbar-nav>
                        </b-collapse>
                    </b-navbar>
                    <b-table striped hover borderless :items="marketData.Listings" :fields="listingFields" :filter="filter" :busy="!marketDataLoaded && marketDataRequested" per-page="50">
                        <template #table-busy>
                            <div class="text-center my-2">
                                <b-spinner class="align-middle"></b-spinner>
                                <strong>Loading...</strong>
                            </div>
                        </template>
                    </b-table>
                </b-tab>
                <b-tab title="Recipes" @click="loadRecipeSuggestions" active lazy>
                    <template #title>
                        Recipes <b-badge v-if="recipeSuggestionsLoaded">{{recipeSuggestions.Suggestions.length}}</b-badge><b-spinner type="border" v-if="!recipeSuggestionsLoaded && recipeSuggestionsRequested" small></b-spinner>
                    </template>
                    <b-navbar toggleable="lg">
                        <b-navbar-brand href="#">Search</b-navbar-brand>
                        <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>

                        <b-collapse id="nav-collapse" is-nav>
                            <b-navbar-nav>
                                <b-input-group class="mr-sm-2">
                                    <b-form-input
                                        id="filter-input"
                                        v-model="filter"
                                        type="search"
                                        placeholder="Type to Search"
                                        debounce="500"
                                    ></b-form-input>

                                    <b-input-group-append>
                                        <b-button :disabled="!filter" @click="filter = ''">Clear</b-button>
                                    </b-input-group-append>
                                </b-input-group>
                            </b-navbar-nav>
                            <b-navbar-nav>
                                <b-nav-form class="mr-sm-2">
                                    <b-form-select id="tradeskill-filter" v-model="tradeskillFilter" :options="tradeskillOptions">
                                        <template #first>
                                            <b-form-select-option :value="null">Any Tradeskill</b-form-select-option>
                                        </template>
                                    </b-form-select>
                                </b-nav-form>

                                <b-nav-form @submit.stop.prevent>
                                    <b-form-input id="level-filter" v-model="levelFilter" type="number" placeholder="Any Level" debounce="500"></b-form-input>
                                </b-nav-form>
                            </b-navbar-nav>
                            <b-navbar-nav class="ml-auto">
                                <b-nav-text>Updated <em>{{recipeSuggestionsUpdated}}</em></b-nav-text>
                            </b-navbar-nav>
                        </b-collapse>
                    </b-navbar>
                    <b-card-group columns>
                        <b-card v-if="!recipeSuggestionsLoaded && recipeSuggestionsRequested">
                            <b-spinner class="align-middle"></b-spinner>
                            <strong>Loading...</strong>
                        </b-card>
                        <b-card v-for="recipeSuggestion in filteredRecipeSuggestions" :key="recipeSuggestion.RecipeId">
                            <b-card-title>{{recipeSuggestion.Name}}</b-card-title>
                            <b-card-sub-title>{{recipeSuggestion.Tradeskill}} {{recipeSuggestion.LevelRequirement}}</b-card-sub-title>
                            </br>
                            <p>
                            Cost: $\{{round(recipeSuggestion.CostPerQuantity)}} ea</br>
                            Efficiency: {{round(recipeSuggestion.ExperienceEfficienyForPrimaryTradekill)}} xp/$
                            </p>
                            <p>
                            Experience
                            <ul>
                                <li v-for="(experience, tradeskill) in recipeSuggestion.TotalExperience" :key="tradeskill">
                                {{experience}} {{tradeskill}}
                                </li>
                            </ul>
                            </p>
                            <p>
                            Ingredients
                            <ul>
                                <li v-for="buy in recipeSuggestion.Buys" :key="recipeSuggestion.RecipeId + buy.Name">
                                Buy x{{buy.Quantity}} {{buy.Name}} for $\{{round(buy.CostPerQuantity)}} ea (x{{buy.Available}})
                                </li>
                                <li v-for="craft in recipeSuggestion.Crafts" :key="recipeSuggestion.RecipeId + craft.Name">
                                Craft x{{craft.Quantity}} {{craft.Name}}
                                    <ul>
                                        <li v-for="level2Buy in craft.Buys" :key="craft.RecipeId + level2Buy.Name">
                                        Buy x{{level2Buy.Quantity}} {{level2Buy.Name}} for $\{{round(level2Buy.CostPerQuantity)}} ea (x{{level2Buy.Available}})
                                        </li>
                                        <li v-for="level2Craft in craft.Crafts" :key="craft.RecipeId + level2Craft.Name">
                                        Craft x{{level2Craft.Quantity}} {{level2Craft.Name}}
                                            <ul>
                                                <li v-for="level3Buy in level2Craft.Buys" :key="craft.RecipeId + level2Craft.RecipeId + level3Buy.Name">
                                                Buy x{{level3Buy.Quantity}} {{level3Buy.Name}} for $\{{round(level3Buy.CostPerQuantity)}} ea (x{{level3Buy.Available}})
                                                </li>
                                                <li v-for="level3Craft in level2Craft.Crafts" :key="craft.RecipeId + level2Craft.RecipeId + level3Craft.Name">
                                                Craft x{{level3Craft.Quantity}} {{level3Craft.Name}}
                                                    <ul>
                                                        <li v-for="level4Buy in level3Craft.Buys" :key="craft.RecipeId + level2Craft.RecipeId + level3Craft.RecipeId + level4Buy.Name">
                                                        Buy x{{level4Buy.Quantity}} {{level4Buy.Name}} for $\{{round(level4Buy.CostPerQuantity)}} ea (x{{level4Buy.Available}})
                                                        </li>
                                                        <li v-for="level4Craft in level3Craft.Crafts" :key="craft.RecipeId + level2Craft.RecipeId + level3Craft.RecipeId + level4Craft.Name">
                                                        Craft x{{level4Craft.Quantity}} {{level4Craft.Name}}
                                                        </li>
                                                    </ul>
                                                </li>
                                            </ul>
                                        </li>
                                    </ul>
                                </li>
                            </ul>
                            </p>
                        </b-card>
                    </b-card-group>
                </b-tab>
                <b-tab title="Items" @click="loadItemTrendData" lazy>
                    <template #title>
                        Items <b-badge v-if="itemTrendDataLoaded">{{itemTrendData.Items.length}}</b-badge><b-spinner type="border" v-if="!itemTrendDataLoaded && itemTrendDataRequested" small></b-spinner>
                    </template>
                    <b-navbar toggleable="lg">
                        <b-navbar-brand href="#">Search</b-navbar-brand>
                        <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>

                        <b-collapse id="nav-collapse" is-nav>
                            <b-navbar-nav>
                                <b-input-group class="mr-sm-2">
                                    <b-form-input
                                        id="filter-input"
                                        v-model="filter"
                                        type="search"
                                        placeholder="Type to Search"
                                        debounce="500"
                                    ></b-form-input>

                                    <b-input-group-append>
                                        <b-button :disabled="!filter" @click="filter = ''">Clear</b-button>
                                    </b-input-group-append>
                                </b-input-group>
                            </b-navbar-nav>
                            <b-navbar-nav class="ml-auto">
                                <b-nav-text>Updated <em>{{itemTrendDataUpdated}}</em></b-nav-text>
                            </b-navbar-nav>
                        </b-collapse>
                    </b-navbar>
                    <b-card-group columns>
                        <b-card v-if="!itemTrendDataLoaded && itemTrendDataRequested">
                            <b-spinner class="align-middle"></b-spinner>
                            <strong>Loading...</strong>
                        </b-card>
                        <b-card v-for="itemTrend in filteredItemTrendDatas" :key="itemTrend.Name" no-body>
                            <b-card-body>
                                <b-card-title>{{itemTrend.Name}}</b-card-title>
                                <b-card-text>
                                    Total Market: $\{{round(itemTrend.DailyStats[0].TotalMarket)}} <strong>({{round(itemTrend.DailyStats[0].TotalMarket - itemTrend.DailyStats[1].TotalMarket)}})</strong>
                                    </br>Min Price: $\{{round(itemTrend.DailyStats[0].MinPrice)}} <strong>({{round(itemTrend.DailyStats[0].MinPrice - itemTrend.DailyStats[1].MinPrice)}})</strong>
                                    </br>Avg Price: $\{{round(itemTrend.DailyStats[0].AveragePrice)}} <strong>({{round(itemTrend.DailyStats[0].AveragePrice - itemTrend.DailyStats[1].AveragePrice)}})</strong>
                                    </br>Max Price: $\{{round(itemTrend.DailyStats[0].MaxPrice)}} <strong>({{round(itemTrend.DailyStats[0].MaxPrice - itemTrend.DailyStats[1].MaxPrice)}})</strong>
                                    </br>Total Available: {{itemTrend.DailyStats[0].TotalAvailable}} <strong>({{round(itemTrend.DailyStats[0].TotalAvailable - itemTrend.DailyStats[1].TotalAvailable)}})</strong>
                                    </br>Total Available For Avg Price: {{itemTrend.DailyStats[0].TotalAvailableBelowMarketAverage}} <strong>({{round(itemTrend.DailyStats[0].TotalAvailableBelowMarketAverage - itemTrend.DailyStats[1].TotalAvailableBelowMarketAverage)}})</strong>
                                </b-card-text>
                            </b-card-body>
                            <b-tabs pills card end>
                                <b-tab title="Average" active><trend :data="itemTrend.DailyStats.map(dailyStat => dailyStat.AveragePrice).reverse()" smooth></trend></b-tab>
                                <b-tab title="Min"><trend :data="itemTrend.DailyStats.map(dailyStat => dailyStat.MinPrice).reverse()" smooth></trend></b-tab>
                                <b-tab title="Max"><trend :data="itemTrend.DailyStats.map(dailyStat => dailyStat.MaxPrice).reverse()" smooth></trend></b-tab>
                                <b-tab title="Available"><trend :data="itemTrend.DailyStats.map(dailyStat => dailyStat.TotalAvailable).reverse()" smooth></trend></b-tab>
                                <b-tab title="Market"><trend :data="itemTrend.DailyStats.map(dailyStat => dailyStat.TotalMarket).reverse()" smooth></trend></b-tab>
                            </b-tabs>
                        </b-card>
                    </b-card-group>
                </b-tab>
            </b-tabs>
        </b-card>
    </b-container>
    `,
    data() {
        return {
            serverKey: null,
            marketDataRequested: false,
            marketDataLoaded: false,
            marketData: {
                Updated: null,
                Listings: [],
            },
            recipeSuggestionsRequested: false,
            recipeSuggestionsLoaded: false,
            recipeSuggestions: {
                Updated: null,
                Suggestions: []
            },
            itemTrendDataRequested: false,
            itemTrendDataLoaded: false,
            itemTrendData: {
                Updated: null,
                Items: []
            },
            listingFields: [
                {
                    key: 'Name',
                    sortable: true,
                },
                {
                    key: 'Price',
                    sortable: true,
                },
                {
                    key: 'Location',
                    sortable: true,
                },
                {
                    key: 'available',
                    sortable: true,
                    sortByFormatted: true,
                    formatter: (value, key, item) => {
                        return item != null ? item.Instances[0].Available : null;
                    }
                },
                {
                    key: 'expires',
                    formatter: (value, key, item) => {
                        let hoursRemaining = this.getHoursFromTimeSpan(item.Instances[0].TimeRemaining);
                        return item != null ? moment(item.Instances[0].Time).add(hoursRemaining, 'h').fromNow() : null;
                    }
                },
                {
                    key: 'lastUpdated',
                    formatter: (value, key, item) => {
                        return item != null ? moment(item.Instances[0].Time).fromNow() : null;
                    }
                }
            ],
            tradeskillFilter: null,
            tradeskillOptions: [
                'Arcana',
                'Armoring',
                'Cooking',
                'Engineering',
                'Furnishing',
                'Jewelcrafting',
                'Weaponsmithing',
            ],
            levelFilter: null,
            filter: null,
            serverList: [
                'Orofena'
            ]
        };
    },
    mounted() {
        this.loadRecipeSuggestions();
    },
    props: {
        server: {
            type: String,
            default: 'Home'
        }
    },
    methods: {
        loadMarketData() {
            if (!this.marketDataLoaded) {
                this.marketDataRequested = true;
                fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/" + this.server.toLowerCase() + "/database.json")
                    .then(response => response.json())
                    .then(data => {
                        this.marketData = data;
                        this.marketDataLoaded = true;
                    });
            }
        },
        loadRecipeSuggestions() {
            if (!this.recipeSuggestionsLoaded) {
                this.recipeSuggestionsRequested = true;
                fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/" + this.server.toLowerCase() + "/recipeSuggestions.json")
                    .then(response => response.json())
                    .then(data => {
                        this.recipeSuggestions = data;
                        this.recipeSuggestionsLoaded = true;
                    });
            }
        },
        loadItemTrendData() {
            if (!this.itemTrendDataLoaded) {
                this.itemTrendDataRequested = true;
                fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/" + this.server.toLowerCase() + "/itemTrendData.json")
                    .then(response => response.json())
                    .then(data => {
                        this.itemTrendData = data;
                        this.itemTrendDataLoaded = true;
                    });
            }
        },
        getHoursFromTimeSpan(timeSpan) {
            if (!timeSpan) {
                return timeSpan;
            }

            let parts = timeSpan.split('.');

            if (parts.length < 1 || parts.length > 2) {
                return timeSpan;
            }

            let smallParts = parts[parts.length - 1].split(':');

            if (smallParts.length != 3) {
                return timeSpan;
            }

            return parseInt(parts[0]) * 24 + parseInt(smallParts[0]);
        },
        round(number) {
            return Math.round(number * 100) / 100;
        },
        changeServer(server) {
            this.server = server;

            this.marketDataRequested = false;
            this.marketDataLoaded = false;
            this.marketData = {
                Updated: null,
                Listings: [],
            };

            this.recipeSuggestionsRequested = false;
            this.recipeSuggestionsLoaded = false;
            this.recipeSuggestions = {
                Updated: null,
                Suggestions: []
            };

            this.itemTrendDataRequested = false;
            this.itemTrendDataLoaded = false;
            this.itemTrendData = {
                Updated: null,
                Items: []
            };

            this.loadRecipeSuggestions();
        }
    },
    computed: {
        filteredRecipeSuggestions() {
            return this.recipeSuggestions.Suggestions
                .filter(recipe => (!this.filter || this.filter.toLowerCase().split(" ").every(filterItem => recipe.Name.toLowerCase().includes(filterItem))) && (!this.tradeskillFilter || recipe.Tradeskill === this.tradeskillFilter) && (!this.levelFilter || recipe.LevelRequirement <= this.levelFilter))
                .sort((a, b) => (a.ExperienceEfficienyForPrimaryTradekill < b.ExperienceEfficienyForPrimaryTradekill) ? 1 : -1)
                .slice(0, 50);
        },
        filteredItemTrendDatas() {
            return this.itemTrendData.Items
                .filter(recipe => (!this.filter || this.filter.toLowerCase().split(" ").every(filterItem => recipe.Name.toLowerCase().includes(filterItem))))
                .sort((a, b) => (a.Name > b.Name) ? 1 : -1)
                .slice(0, 50);
        },
        marketDataUpdated() {
            return this.marketData.Updated ? moment(this.marketData.Updated).fromNow() : 'Never';
        },
        recipeSuggestionsUpdated() {
            return this.recipeSuggestions.Updated ? moment(this.recipeSuggestions.Updated).fromNow() : 'Never';
        },
        itemTrendDataUpdated() {
            return this.itemTrendData.Updated ? moment(this.itemTrendData.Updated).fromNow() : 'Never';
        }
    }
};