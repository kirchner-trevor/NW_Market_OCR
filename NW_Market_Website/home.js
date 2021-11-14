export default {
    name: 'home',
    template: /*html*/`
    <b-container class="mt-3" fluid>
        <b-jumbotron header="NW Market - Orofena" lead="View market listings, recipes, and more to come!">
            <p>Interesting in contributing to the project? Download the market collector!</p>
            <b-button variant="primary" href="/NW_Market_Collector.zip" download>Download</b-button>
        </b-jumbotron>
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
                            Cost: $\{{Math.round(recipeSuggestion.CostPerQuantity * 100) / 100}} ea</br>
                            Efficiency: {{Math.round(recipeSuggestion.ExperienceEfficienyForPrimaryTradekill * 100) / 100}} xp/$
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
                                Buy x{{buy.Quantity}} {{buy.Name}} for $\{{buy.CostPerQuantity}} ea @ {{buy.Location}} (x{{buy.Available}})
                                </li>
                                <li v-for="craft in recipeSuggestion.Crafts" :key="recipeSuggestion.RecipeId + craft.Name">
                                Craft x{{craft.Quantity}} {{craft.Name}}
                                    <ul>
                                        <li v-for="level2Buy in craft.Buys" :key="craft.RecipeId + level2Buy.Name">
                                        Buy x{{level2Buy.Quantity}} {{level2Buy.Name}} for $\{{level2Buy.CostPerQuantity}} ea @ {{level2Buy.Location}} (x{{level2Buy.Available}})
                                        </li>
                                        <li v-for="level2Craft in craft.Crafts" :key="craft.RecipeId + level2Craft.Name">
                                        Craft x{{level2Craft.Quantity}} {{level2Craft.Name}}
                                            <ul>
                                                <li v-for="level3Buy in level2Craft.Buys" :key="craft.RecipeId + level2Craft.RecipeId + level3Buy.Name">
                                                Buy x{{level3Buy.Quantity}} {{level3Buy.Name}} for $\{{level3Buy.CostPerQuantity}} ea @ {{level3Buy.Location}} (x{{level3Buy.Available}})
                                                </li>
                                                <li v-for="level3Craft in level2Craft.Crafts" :key="craft.RecipeId + level2Craft.RecipeId + level3Craft.Name">
                                                Craft x{{level3Craft.Quantity}} {{level3Craft.Name}}
                                                    <ul>
                                                        <li v-for="level4Buy in level3Craft.Buys" :key="craft.RecipeId + level2Craft.RecipeId + level3Craft.RecipeId + level4Buy.Name">
                                                        Buy x{{level4Buy.Quantity}} {{level4Buy.Name}} for $\{{level4Buy.CostPerQuantity}} ea @ {{level4Buy.Location}} (x{{level4Buy.Available}})
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
            </b-tabs>
        </b-card>
    </b-container>
    `,
    data() {
        return {
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
            filter: null
        };
    },
    mounted() {
        this.loadRecipeSuggestions();
    },
    methods: {
        loadMarketData() {
            if (!this.marketDataLoaded) {
                this.marketDataRequested = true;
                fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/database.json")
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
                fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/recipeSuggestions.json")
                    .then(response => response.json())
                    .then(data => {
                        this.recipeSuggestions = data;
                        this.recipeSuggestionsLoaded = true;
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
        }
    },
    computed: {
        filteredRecipeSuggestions() {
            return this.recipeSuggestions.Suggestions
                .filter(recipe => (!this.filter || this.filter.toLowerCase().split("\s").some(filterItem => recipe.Name.toLowerCase().includes(filterItem))) && (!this.tradeskillFilter || recipe.Tradeskill === this.tradeskillFilter) && (!this.levelFilter || recipe.LevelRequirement <= this.levelFilter))
                .sort((a, b) => (a.ExperienceEfficienyForPrimaryTradekill < b.ExperienceEfficienyForPrimaryTradekill) ? 1 : -1);
        },
        marketDataUpdated() {
            return this.marketData.Updated ? moment(this.marketData.Updated).fromNow() : 'Never';
        },
        recipeSuggestionsUpdated() {
            return this.recipeSuggestions.Updated ? moment(this.recipeSuggestions.Updated).fromNow() : 'Never';
        }
    }
};