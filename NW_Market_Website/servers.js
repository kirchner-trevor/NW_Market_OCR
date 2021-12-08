export default {
    name: 'servers',
    template: /*html*/`
    <b-container class="mt-3" fluid>
        <b-navbar toggleable="lg">
            <b-navbar-brand href="#/"><h2><strong>NW Market</strong></h2></b-navbar-brand>
        </b-navbar>
        <b-card class="bg-light">
            <p><em>View market listings, recipes, and more to come!</em></p>
            <p>Interesting in contributing to the project? Check out the code on <b-link href="https://github.com/kirchner-trevor/NW_Market_OCR">GitHub</b-link>!</p>
            <p>You can also download the market collector to help keep the site up to date!</p>
            <b-button variant="primary" href="https://github.com/kirchner-trevor/NW_Market_OCR/releases/latest">Download</b-button>
        </b-card>
        </br>
        <b-card no-body>
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
                        <b-nav-text>Updated <em>{{configurationDataUpdated}}</em></b-nav-text>
                    </b-navbar-nav>
                </b-collapse>
            </b-navbar>
            <b-table striped hover borderless :items="configurationData.ServerList" :fields="serverFields" :filter="filter" sort-by="Listings" sort-desc select-mode="single" selectable @row-selected="onRowSelected" :busy="!configurationDataLoaded && configurationDataRequested" per-page="50">
                <template #table-busy>
                    <div class="text-center my-2">
                        <b-spinner class="align-middle"></b-spinner>
                        <strong>Loading...</strong>
                    </div>
                </template>
            </b-table>
        </b-card>
    </b-container>
    `,
    data() {
        return {
            configurationDataRequested: false,
            configurationDataLoaded: false,
            configurationData: {
                Updated: null,
                ServerList: []
            },
            filter: null,
            serverFields: [
                {
                    key: 'Name',
                    sortable: true,
                },
                {
                    key: 'WorldSet',
                    sortable: true,
                },
                {
                    key: 'Region',
                    sortable: true,
                },
                {
                    key: 'Listings',
                    sortable: true,
                    sortByFormatted: true,
                    formatter: (value, key, item) => {
                        return value != null ? value : 0;
                    }
                },
            ],
        };
    },
    mounted() {
        this.loadConfigurationData();
    },
    methods: {
        loadConfigurationData() {
            if (!this.configurationDataLoaded) {
                this.configurationDataRequested = true;
                fetch("https://nwmarketdata.s3.us-east-2.amazonaws.com/configurationData.json")
                    .then(response => response.json())
                    .then(data => {
                        this.configurationData = data;
                        this.configurationDataLoaded = true;
                    });
            }
        },
        onRowSelected(items) {
            let serverSelected = items.length > 0 ? items[0] : null;
            if (serverSelected != null && serverSelected.Listings) {
                this.$router.push('/' + serverSelected.Id + '/');
            }
        }
    },
    computed: {
        configurationDataUpdated() {
            return this.configurationData.Updated ? moment(this.configurationData.Updated).fromNow() : 'Never';
        },
    }
};