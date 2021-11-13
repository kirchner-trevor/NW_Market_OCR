export default {
    name: 'home',
    template: /*html*/`
    <b-container class="mt-3" fluid>
        <b-jumbotron header="NW Market - Orofena" lead="View market listings and more to come!">
        </b-jumbotron>
        <b-card no-body>
            <b-tabs card>
                <b-tab title="Listings" @click="loadMarketData">
                    <b-row align-h="start">
                        <b-col cols="2">
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
                        </b-col>
                        <b-col>
                        Updated <i>{{marketDataUpdated}}</i>
                        </b-col>
                    </b-row>
                    <b-table striped hover borderless :items="marketData.Listings" :fields="listingFields" :filter="filter"></b-table>
                </b-tab>
                <b-tab title="Recipes" @click="loadRecipeSuggestions" active>
                    <b-input-group>
                        <b-row align-h="start">
                            <b-col>
                                <b-form-select id="tradeskill-filter" v-model="tradeskillFilter" :options="tradeskillOptions">
                                    <template #first>
                                        <b-form-select-option :value="null">-- Any Tradeskill --</b-form-select-option>
                                    </template>
                                </b-form-select>
                            </b-col>
                                
                            <b-col>
                                <b-form-input id="level-filter" v-model="levelFilter" type="number" placeholder="Level"></b-form-input>
                            </b-col>

                            <b-col>
                            Updated <i>{{recipeSuggestionsUpdated}}</i>
                            </b-col>
                        </b-row>
                    </b-input-group>
                    </br>
                    <b-card-group columns>
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
                                        <li v-for="innerBuy in craft.Buys" :key="craft.RecipeId + innerBuy.Name">
                                        Buy x{{innerBuy.Quantity}} {{innerBuy.Name}} for $\{{innerBuy.CostPerQuantity}} ea @ {{innerBuy.Location}} (x{{innerBuy.Available}})
                                        </li>
                                        <li v-for="innerCraft in craft.Crafts" :key="craft.RecipeId + innerCraft.Name">
                                        Craft x{{innerCraft.Quantity}} {{innerCraft.Name}}
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
            marketDataLoaded: false,
            marketData: {
                Updated: null,
                Listings: [],
            },
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
                .filter(recipe => (!this.tradeskillFilter || recipe.Tradeskill === this.tradeskillFilter) && (!this.levelFilter || recipe.LevelRequirement <= this.levelFilter))
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